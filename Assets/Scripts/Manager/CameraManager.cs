using System.Collections;
using Cinemachine;
using DG.Tweening;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MyGame.CameraControl
{
    public class CameraManager : MonoBehaviour
    {
        [InfoBox(
            "このスクリプトはDebugSceneでも用います。\nそのため、プレハブしておいてください。"
        )]
        [ReadOnly]
        [SerializeField]
        private string _instruction = "設定不要";

        [SerializeField]
        private NoiseSettings takeHitNoiseSettings; // 敵ヒット時の揺れ設定をInspectorから割り当てるための変数

        [SerializeField]
        [Tooltip("敵ヒット時の揺れの強さ（振幅）")]
        private float hitShakeAmplitude = 0.35f;

        [SerializeField]
        [Tooltip("敵ヒット時の揺れの細かさ（周波数）")]
        private float hitShakeFrequency = 2.0f;
        public static CameraManager instance { get; private set; }
        private Camera cam;
        private CinemachineVirtualCamera virtualCamera;
        private CinemachineTransposer framing;
        private CameraBoundaryChecker boundaryChecker;
        private CinemachineBasicMultiChannelPerlin perlinNoise;
        private const float HIT_SHAKE_DURATION = 0.1f; // 敵ヒット時0.1秒間揺らす
        private Coroutine shakeCoroutine = null; // 実行中のシェイクコルーチンを管理
        private Coroutine dampingResetCoroutine = null; // 実行中のダンピングリセットコルーチンを管理するための変数
        private bool isPriorityShakeActive = false; // 優先度の高い（カスタム）シェイクが実行中かどうかを示すフラグ
        public bool IsTimelineControlMode { get; private set; } = false; // 外部からTimelineモードかを確認するためのプロパティ
        private Tweener lensTween;
        private Tweener offsetTween;

        // --- タイムライン制御用変数 ---
        private float timelineAmplitude = 0f;
        private float timelineFrequency = 0f;
        private GameObject timelineTargetObject; // Timeline追従用のダミーオブジェクト
        private Transform originalFollowTarget; // 元の追尾対象（プレイヤー等）
        private float originalXDamping; // 元のDamping設定保存用
        private float originalYDamping; // 元のDamping設定保存用
        private bool isDebugScene = false; // 開発用フラグ：デバッグシーンかどうか
        #region Unity Methods /// <summary>
        private void Awake()
        {
            if (instance == null)
            {
                instance = this;

#if UNITY_EDITOR
                // デバッグシーンかどうかを判定
                isDebugScene = SceneManager.GetActiveScene().name.Contains("Debug");
#endif

                // 自動でMain Cameraを取得
                if (cam == null)
                {
                    cam = Camera.main;
                }

                // CameraBoundaryCheckerを取得
                if (cam != null)
                {
                    boundaryChecker = cam.GetComponent<CameraBoundaryChecker>();
                }

                if (cam == null)
                {
                    Debug.LogError("CameraManagerはMain Cameraを取得できませんでした");
                }

                if (boundaryChecker == null
#if UNITY_EDITOR
                    && !isDebugScene
#endif
                )
                {
                    Debug.LogError("CameraManagerはCameraBoundaryCheckerを取得できませんでした");
                }

                // 自動でCinemachineVirtualCameraを取得
                if (virtualCamera == null)
                {
                    virtualCamera = FindObjectOfType<CinemachineVirtualCamera>();
                }

                if (virtualCamera == null)
                {
                    Debug.LogError("CameraManagerはカメラに関する要素を取得できませんでした");
                    return;
                }
                else
                {
                    virtualCamera.enabled = false;
                    // Virtual Cameraを初期状態では無効化
                    //CameraBoundaryCheckerで有効化される

                    timelineTargetObject = new GameObject("TimelineCameraTarget");
                    timelineTargetObject.transform.SetParent(this.transform);

#if UNITY_EDITOR
                    if (isDebugScene)
                    {
                        virtualCamera.enabled = true; // デバッグシーンでは最初から有効化しておく
                    }
#endif
                }

                // CinemachineTransposerを取得
                framing = virtualCamera.GetCinemachineComponent<CinemachineTransposer>();
                if (framing != null)
                {
                    framing.m_YDamping = GameConstants.CAMERA_FOLLOW_DAMPING_Y; // 初期のYDamping値を設定
                }
                else
                {
#if UNITY_EDITOR
                    if (!isDebugScene)
                    {
                        Debug.LogError(
                            "CameraManagerはCinemachineTransposerを取得できませんでした"
                        );
                    }
#endif
                }

                // VCamからNoiseコンポーネントを取得
                perlinNoise =
                    virtualCamera.GetCinemachineComponent<CinemachineBasicMultiChannelPerlin>();
                if (perlinNoise == null)
                {
                    // このエラーが出た場合、VCamにNoiseコンポーネントを追加し、Profileを設定してください
                    Debug.LogError(
                        "CameraManagerはCinemachineBasicMultiChannelPerlinを取得できませんでした。ダメージ時の揺れは機能しません。"
                    );
                }

                if (takeHitNoiseSettings == null)
                {
                    takeHitNoiseSettings = Resources.Load<NoiseSettings>($"EnemyHitShake");
                    if (takeHitNoiseSettings == null)
                    {
                        Debug.LogWarning(
                            "CameraManagerのtakeHitNoiseSettingsが設定されていません。ダメージ時の揺れは機能しません。"
                        );
                    }
                    else
                    {
                        Debug.Log("takeHitNoiseSettingsをResourcesから読み込みました。");
                    }
                }
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            if (perlinNoise != null)
            {
                // 初めは無効化しておく
                // Awakeで行うと、"none"状態になってしまう場合があるため、Startで行う
                // perlinNoise.enabled = false;
                perlinNoise.m_NoiseProfile = null;
                perlinNoise.m_AmplitudeGain = 0f;
                perlinNoise.m_FrequencyGain = 0f;
            }
        }
        #endregion

        #region Camera Movement Logic
        /// <summary>
        /// （コルーチン）カメラのY軸追従を即座に行わせ（Damping=0）、ターゲットに十分近づくかカメラが端に達するまで待機し、
        ///  その後Damping設定を元に戻します。タイムアウト付き。
        /// </summary>
        public IEnumerator CameraMove()
        {
            if (framing != null)
            {
                // YDampingを0にして即座にプレイヤー位置に追従させる
                framing.m_YDamping = 0;
                yield return null; // 1フレーム待ってCinemachineが位置を更新するのを待つ

                float timeElapsed = 0f;
                float timeOut = 0.5f; // 最大待機時間（秒）。これを超えたら強制的にループを抜ける

                while (true) // ループ自体は常にtrueにし、中のbreakで抜ける
                {
                    timeElapsed += Time.unscaledDeltaTime; // 時間計測

                    Vector3 cameraPos = Camera.main.transform.position;
                    Vector3 targetPos = framing.FollowTargetPosition;

                    // Z軸を無視してXY平面だけの距離を計算する（2Dゲームの場合、Z軸のズレで判定失敗するのを防ぐ）
                    float distanceXY = Vector2.Distance(
                        new Vector2(cameraPos.x, cameraPos.y),
                        new Vector2(targetPos.x, targetPos.y)
                    );

                    // 条件1：カメラとターゲットの距離が閾値以下になったら
                    // targetPosはオフセット込みの位置なので、理想的には距離0になるはずだが、余裕を持って判定
                    bool isCloseEnough = distanceXY <= 0.1f;

                    // 条件2：カメラが移動範囲の端におり、かつX座標の差が閾値以下になったらループを抜ける
                    bool isAtEdge = boundaryChecker.CameraAtEdge != null;

                    // 条件3：タイムアウト時間を超えたら強制終了（無限ループ防止）
                    bool isTimeOut = timeElapsed >= timeOut;

                    if (isCloseEnough || isAtEdge || isTimeOut)
                    {
                        if (isTimeOut)
                        {
                            // ログを出したくない場合はコメントアウトしてください
                            Debug.LogWarning("CameraMoveがタイムアウトしました。強制終了します。");
                        }
                        break; // いずれかの条件を満たしたら待機を終了
                    }

                    yield return null; // 条件を満たさない場合は1フレーム待つ
                }

                framing.m_YDamping = GameConstants.CAMERA_FOLLOW_DAMPING_Y; // 元のYDamping値に戻す
            }
            else
            {
                Debug.LogError("CinemachineTransposerが見つかりません。カメラの追従ができません。");
            }
        }

        /// <summary>
        /// Cinemachine Brainを一時的に無効化し、DOTweenを使用してカメラを指定のターゲット地点まで指定時間で移動させます。
        /// </summary>
        /// <param name="targetPoint">移動先の座標</param>
        /// <param name="reachTime">移動にかかる時間（秒）</param>
        public IEnumerator CameraMoveByTween(Vector3 targetPoint, float reachTime)
        {
            if (cam == null)
                yield break;

            var brain = cam.GetComponent<CinemachineBrain>();
            if (brain != null)
                brain.enabled = false;

            yield return cam
                .transform.DOLocalMove(
                    new Vector3(targetPoint.x, targetPoint.y, cam.transform.position.z),
                    reachTime
                )
                .WaitForCompletion();
        }
        #endregion

        #region Camera Shake Logic
        /// <summary>
        /// 敵ヒット時など、小規模なカメラシェイク（Noise）を発生させます。
        /// </summary>
        public void PlayHitShake()
        {
            if (perlinNoise == null)
            {
                Debug.LogWarning(
                    "Noiseコンポーネントが未設定のため、PlayHitShakeを呼び出せません。"
                );
                return;
            }

            // 優先度の高い（カスタム）シェイクが実行中なら、この（ヒット）シェイクは実行しない
            if (isPriorityShakeActive)
            {
                return;
            }

            // 既に実行中のシェイクコルーチンがあれば停止（連続ヒット時に揺れ時間をリセットするため）
            if (shakeCoroutine != null)
            {
                StopCoroutine(shakeCoroutine);
            }

            // 新しいシェイクコルーチンを開始
            shakeCoroutine = StartCoroutine(ShakeCoroutine());
        }

        /// <summary>
        /// （コルーチン）指定時間だけPerlinNoiseを有効化し、その後無効化します。
        /// </summary>
        private IEnumerator ShakeCoroutine()
        {
            if (takeHitNoiseSettings == null)
            {
                yield break;
            }

            //Noiseプロファイルを設定
            perlinNoise.m_NoiseProfile = takeHitNoiseSettings;
            perlinNoise.m_AmplitudeGain = hitShakeAmplitude;
            perlinNoise.m_FrequencyGain = hitShakeFrequency;

            // 指定時間待機
            yield return new WaitForSeconds(HIT_SHAKE_DURATION);

            // Noiseを無効化（揺れ停止）
            // perlinNoise.enabled = false;
            perlinNoise.m_NoiseProfile = null;
            perlinNoise.m_AmplitudeGain = 0f;
            perlinNoise.m_FrequencyGain = 0f;

            // 管理変数をクリア
            shakeCoroutine = null;
        }

        /// <summary>
        /// 外部から指定された強さと時間で、優先度の高いカメラシェイク（Noise）を発生させます。
        /// この揺れは、PlayHitShake()による揺れをブロックします。
        /// </summary>
        /// <param name="amplitude">揺れの強さ（振幅）</param>
        /// <param name="frequency">揺れの細かさ（周波数）</param>
        /// <param name="duration">揺れる時間（秒）</param>
        public void PlayCustomShake(float amplitude, float frequency, float duration)
        {
            if (perlinNoise == null)
            {
                Debug.LogWarning(
                    "Noiseコンポーネントが未設定のため、PlayCustomShakeを呼び出せません。"
                );
                return;
            }

            // 既に実行中のシェイクコルーチンがあれば停止（カスタムシェイクが常に優先）
            if (shakeCoroutine != null)
            {
                StopCoroutine(shakeCoroutine);
            }

            // 優先シェイクフラグを立て、新しいコルーチンを開始
            isPriorityShakeActive = true;
            shakeCoroutine = StartCoroutine(CustomShakeCoroutine(amplitude, frequency, duration));
        }

        /// <summary>
        /// （コルーチン）指定されたパラメータでPerlinNoiseを有効化し、時間経過後に停止します。
        /// </summary>
        private IEnumerator CustomShakeCoroutine(float amplitude, float frequency, float duration)
        {
            if (takeHitNoiseSettings == null)
            {
                isPriorityShakeActive = false; // 実行できないのでフラグを倒す
                yield break;
            }

            // Noiseを有効化
            // perlinNoise.enabled = true;
            perlinNoise.m_NoiseProfile = takeHitNoiseSettings; // ヒット時と同じNoise Profileを流用

            // パラメータを適用
            perlinNoise.m_AmplitudeGain = amplitude;
            perlinNoise.m_FrequencyGain = frequency;

            // 指定時間待機 (Time.timeScaleの影響を受けます)
            yield return new WaitForSeconds(duration);

            // Noiseを無効化
            // perlinNoise.enabled = false;
            perlinNoise.m_NoiseProfile = null;
            perlinNoise.m_AmplitudeGain = 0f;
            perlinNoise.m_FrequencyGain = 0f;

            // 管理変数をクリア
            shakeCoroutine = null;
            isPriorityShakeActive = false; // 優先シェイクフラグを倒す
        }

        /// <summary>
        /// カメラシェイクのコルーチンを開始します。
        /// </summary>
        /// <param name="positionStrength">シェイクの強さ（各軸）</param>
        /// <param name="shakeDuration">シェイクの時間（秒）</param>
        public void StartCameraShake(Vector3 positionStrength, float shakeDuration)
        {
            StartCoroutine(CameraShake(positionStrength, shakeDuration));
        }

        /// <summary>
        /// （コルーチン）Cinemachine Brainを一時的に無効化し、DOTweenを使用してカメラを振動させます。完了後、CameraResetを呼び出します。
        /// </summary>
        /// <param name="positionStrength">シェイクの強さ（各軸）</param>
        /// <param name="shakeDuration">シェイクの時間（秒）</param>
        public IEnumerator CameraShake(Vector3 positionStrength, float shakeDuration)
        {
            if (cam == null)
                yield break;

            var brain = cam.GetComponent<CinemachineBrain>();
            if (brain != null)
                brain.enabled = false;
            cam.DOComplete();

            yield return cam.DOShakePosition(shakeDuration, positionStrength).WaitForCompletion();
            CameraReset();
        }

        /// <summary>
        /// カメラのCinemachine Brainを再度有効にし、Cinemachineによる通常のカメラ制御に戻します。
        /// </summary>
        public void CameraReset()
        {
            Camera.main.GetComponent<CinemachineBrain>().enabled = true;
        }

        #endregion

        #region Damping Control

        /// <summary>
        /// 指定された時間だけ、カメラのY軸追従のDampingを0にし、即座に追従するようにします。
        /// </summary>
        /// <param name="duration">Dampingを0にしておく時間（秒）</param>
        public void TriggerTemporaryDampingReset(float duration)
        {
            // 既に実行中のリセットコルーチンがあれば、一度停止する
            if (dampingResetCoroutine != null)
            {
                StopCoroutine(dampingResetCoroutine);
            }
            // 新しいリセットコルーチンを開始する
            dampingResetCoroutine = StartCoroutine(TemporaryResetYDampingCoroutine(duration));
        }

        /// <summary>
        /// （コルーチン）指定された時間、CinemachineTransposerのYDampingを0に設定し、時間が経過したら元の値に戻します。
        /// </summary>
        /// <param name="duration">Dampingを0にしておく時間（秒）</param>
        private IEnumerator TemporaryResetYDampingCoroutine(float duration)
        {
            if (framing != null)
            {
                // YDampingを0にして即座に追従させる
                framing.m_YDamping = 0;

                // 指定された時間だけ待つ
                yield return new WaitForSecondsRealtime(duration);

                // 元のYDamping値に戻す
                framing.m_YDamping = GameConstants.CAMERA_FOLLOW_DAMPING_Y;
            }
            else
            {
                if (!isDebugScene)
                {
                    Debug.LogError("CinemachineTransposerが見つかりません。");
                }
            }

            // コルーチンの管理変数をクリア
            dampingResetCoroutine = null;
        }
        #endregion
        #region Camera Settings Override
        /// <summary>
        /// カメラのレンズ設定と追従オフセットを変更します。
        /// </summary>
        /// <param name="orthoSize">ターゲットとなるOrthographic Size</param>
        /// <param name="nearClip">ターゲットとなるNear Clip Plane</param>
        /// <param name="offset">ターゲットとなるFollow Offset</param>
        /// <param name="duration">変更にかける時間（秒）。0の場合は即時変更。</param>
        public void SetCameraSettings(
            float orthoSize,
            float nearClip,
            Vector3 offset,
            float duration = 1.0f
        )
        {
            if (virtualCamera == null || framing == null)
                return;

            // 既存のTweenがあればキルする
            lensTween?.Kill();
            offsetTween?.Kill();

            if (duration <= 0f)
            {
                // 即時変更
                virtualCamera.m_Lens.OrthographicSize = orthoSize;
                virtualCamera.m_Lens.NearClipPlane = nearClip;
                framing.m_FollowOffset = offset;
            }
            else
            {
                // DOTweenで滑らかに変更
                lensTween = DOTween
                    .To(
                        () => virtualCamera.m_Lens.OrthographicSize,
                        x => virtualCamera.m_Lens.OrthographicSize = x,
                        orthoSize,
                        duration
                    )
                    .SetUpdate(true); // TimeScaleの影響を受けないようにする場合

                // NearClipは通常即時変更で問題ないが、必要ならここもTween可能
                virtualCamera.m_Lens.NearClipPlane = nearClip;

                offsetTween = DOTween
                    .To(
                        () => framing.m_FollowOffset,
                        x => framing.m_FollowOffset = x,
                        offset,
                        duration
                    )
                    .SetUpdate(true);
            }
        }

        /// <summary>
        /// カメラの設定をGameConstantsのデフォルト値に戻します。
        /// </summary>
        /// <param name="duration">戻すのにかける時間（秒）</param>
        public void ResetCameraSettings(float duration = 1.0f)
        {
            SetCameraSettings(
                GameConstants.DEFAULT_CAMERA_ORTHO_SIZE,
                GameConstants.DEFAULT_CAMERA_NEAR_CLIP,
                GameConstants.PLAYER_CAMERA_FOLLOW_OFFSET,
                duration
            );
        }
        #endregion

        #region Timeline Control
        /// <summary>
        /// Timelineによるカメラ制御モードを設定します。
        /// Brainは切らず、Followターゲットをダミーに差し替えることで制御権を奪います。
        /// </summary>
        public void SetTimelineControlMode(bool isTimelineControlling)
        {
            if (virtualCamera == null || framing == null || timelineTargetObject == null)
                return;

            IsTimelineControlMode = isTimelineControlling; // 外部から確認できるようにプロパティにセット

            if (isTimelineControlling)
            {
                // まだTimelineモードでなければ（初回突入時）、現在の設定を保存して切り替え
                if (virtualCamera.Follow != timelineTargetObject.transform)
                {
                    // 1. 現在の状態を保存
                    originalFollowTarget = virtualCamera.Follow;
                    originalXDamping = framing.m_XDamping;
                    originalYDamping = framing.m_YDamping;

                    // 2. ダミーを現在のカメラ位置（またはターゲット位置）に同期させる
                    // ※カメラ位置に合わせるのが一番ズレない
                    Vector3 currentPos = cam.transform.position;
                    currentPos.z = GameConstants.PLAYER_CAMERA_FOLLOW_OFFSET.z; // Zは規定値に戻す
                    timelineTargetObject.transform.position = currentPos;

                    // 3. 追尾対象をダミーに変更
                    virtualCamera.Follow = timelineTargetObject.transform;

                    // 4. Timelineの動きに即座に反応させるため、Damping（遅延）をゼロにする
                    framing.m_XDamping = 0f;
                    framing.m_YDamping = 0f;
                }
            }
            else
            {
                // Timelineモード終了。元に戻す
                if (originalFollowTarget != null)
                {
                    virtualCamera.Follow = originalFollowTarget;
                }

                // Dampingを復元
                framing.m_XDamping = originalXDamping;
                framing.m_YDamping = originalYDamping;
            }

            // Debug.Log($"[CameraManager] TimelineMode: {isTimelineControlling}");
        }

        /// <summary>
        /// カメラ（の追尾対象）を指定の座標に移動させます。
        /// </summary>
        public void SetCameraPosition(Vector2 position)
        {
            if (timelineTargetObject == null)
                return;

            // ダミーオブジェクトを移動させる
            // Cinemachineがこれを追うので、カメラも動く
            Vector3 newPos = new Vector3(
                position.x,
                position.y,
                timelineTargetObject.transform.position.z
            );
            timelineTargetObject.transform.position = newPos;
        }

        /// <summary>
        /// Timelineからの振動指示
        /// Brainが生きているので、単純にパラメータをセットするだけでOK
        /// </summary>
        public void SetTimelineShake(float amplitude, float frequency)
        {
            timelineAmplitude = amplitude;
            timelineFrequency = frequency;
            ApplyShake();
        }

        /// <summary>
        /// Timelineからの振動指示を適用します。
        /// </summary>
        private void ApplyShake()
        {
            if (perlinNoise == null)
                return;

            // Timelineの指定があれば適用
            if (timelineAmplitude > 0.001f)
            {
                perlinNoise.m_AmplitudeGain = timelineAmplitude;
                perlinNoise.m_FrequencyGain = timelineFrequency;

                // プロファイルがなければセット
                if (perlinNoise.m_NoiseProfile == null && takeHitNoiseSettings != null)
                {
                    perlinNoise.m_NoiseProfile = takeHitNoiseSettings;
                }
            }
            else
            {
                // Timeline指定なし、かつヒットシェイクもなければ0にする
                if (shakeCoroutine == null && !isPriorityShakeActive)
                {
                    perlinNoise.m_AmplitudeGain = 0f;
                    perlinNoise.m_FrequencyGain = 0f;
                }
            }
        }
        #endregion
    }
}
