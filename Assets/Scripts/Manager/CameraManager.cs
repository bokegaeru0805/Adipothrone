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
#pragma warning disable 0414 // 使われていない変数の警告（CS0414）を一時的に無効化
        [InfoBox(
            "このスクリプトはDebugSceneでも用います。\nそのため、プレハブしておいてください。"
        )]
        [ReadOnly]
        [SerializeField]
        private string _instruction = "設定不要";
#pragma warning restore 0414 // 警告の無効化を解除（これ以降のコードでは通常通り警告を出す）
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
        private CinemachineConfiner2D confiner;
        private const float HIT_SHAKE_DURATION = 0.1f; // 敵ヒット時0.1秒間揺らす
        private Coroutine shakeCoroutine = null; // 実行中のシェイクコルーチンを管理
        private Coroutine dampingResetCoroutine = null; // 実行中のダンピングリセットコルーチンを管理するための変数
        private bool isPriorityShakeActive = false; // 優先度の高い（カスタム）シェイクが実行中かどうかを示すフラグ
        public bool IsTimelineControlMode { get; private set; } = false; // 外部からTimelineモードかを確認するためのプロパティ
        private Tweener lensTween;
        private Tweener offsetTween;
        private Tweener xDampingTween;
        private Tweener yDampingTween;
        public bool IsContinuousShakeActive { get; private set; } // 持続シェイクが実行中か
        private Tweener continuousShakeTween; // フェードアウト用のTween
        private bool isManualContinuousShakeActive; // Cinemachine Brain停止中に実カメラを揺らしているか
        private float manualShakeAmplitude;
        private float manualShakeFrequency;
        private Vector3 manualShakeStartPosition;
        private Vector2 manualShakeEndPosition;

        // 現在設定されているDampingの基準値を保持する変数
        private float currentBaseXDamping = GameConstants.CAMERA_FOLLOW_DAMPING_X;
        private float currentBaseYDamping = GameConstants.CAMERA_FOLLOW_DAMPING_Y;

        // --- タイムライン制御用変数 ---
        private float timelineAmplitude = 0f;
        private float timelineFrequency = 0f;
        private GameObject timelineTargetObject; // Timeline追従用のダミーオブジェクト
        private Transform originalFollowTarget; // 元の追尾対象（プレイヤー等）
        private float originalXDamping; // 元のDamping設定保存用
        private float originalYDamping; // 元のDamping設定保存用
        private bool isDebugScene = false; // 開発用フラグ：デバッグシーンかどうか

        // --- エリアロック制御用変数 ---
        private GameObject areaLockTargetObject; // エリアロック時の追従用ダミーオブジェクト
        private bool isAreaLocked = false; // エリアロック中かどうかのフラグ
        #region Unity Methods
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

                    areaLockTargetObject = new GameObject("AreaLockCameraTarget");
                    areaLockTargetObject.transform.SetParent(this.transform);

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
                    currentBaseYDamping = GameConstants.CAMERA_FOLLOW_DAMPING_Y;
                    framing.m_YDamping = currentBaseYDamping; // 初期のYDamping値を設定
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

                // CinemachineConfiner2Dを取得
                confiner = virtualCamera.GetComponent<CinemachineConfiner2D>();
                if (confiner == null)
                {
                    Debug.LogError(
                        "CameraManagerはCinemachineConfiner2Dを取得できませんでした。カメラのエリア制限が機能しません。"
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

        private void LateUpdate()
        {
            if (!isManualContinuousShakeActive || cam == null)
                return;

            float time = Time.unscaledTime * manualShakeFrequency;
            float offsetX = (Mathf.PerlinNoise(time, 0f) * 2f - 1f) * manualShakeAmplitude;
            float offsetY = (Mathf.PerlinNoise(0f, time) * 2f - 1f) * manualShakeAmplitude;

            cam.transform.localPosition = new Vector3(
                manualShakeStartPosition.x + offsetX,
                manualShakeStartPosition.y + offsetY,
                manualShakeStartPosition.z
            );
        }
        #endregion

        #region Camera Movement Logic
        /// <summary>
        /// （コルーチン）カメラの追従を即座に行わせ（Damping=0）、ターゲットに重なるまで待機します。
        /// ワープ移動時の座標ズレやタイムアウトを防ぐため、強制的な位置同期を行います。
        /// </summary>
        public IEnumerator CameraMove()
        {
            if (framing != null)
            {
                // // 1. 元の設定値を保持
                // float prevXDamping = framing.m_XDamping;
                // float prevYDamping = framing.m_YDamping;

                // // 2. Dampingを無効化して即時追従モードにする
                // framing.m_XDamping = 0;
                // framing.m_YDamping = 0;

                // // Cinemachineにワープを通知（内部演算のリセット）
                // virtualCamera.OnTargetObjectWarped(
                //     framing.FollowTarget,
                //     framing.FollowTargetPosition - cam.transform.position
                // );

                // float timeElapsed = 0f;
                // float timeOut = 2.0f;

                // // 【追加】カメラが静止したことを検知するための変数
                // Vector3 lastCamPos = cam.transform.position;

                // // 【重要】CameraMoveAreaのConfiner更新（最大10フレーム程度かかる）を待つため、
                // // 最低でもこの時間は強制同期を続け、完了判定を行わないようにする。
                // // これにより「古いエリアの端」で誤って完了判定され、カメラが置き去りになるのを防ぐ。
                // float minDuration = 0.25f;

                // while (true)
                // {
                //     timeElapsed += Time.unscaledDeltaTime;

                //     // 3. 毎フレーム強制的にカメラ位置をターゲット位置へ移動させる
                //     // Confinerが更新されるまでの間も位置合わせを試行し続けることで、
                //     // 更新された瞬間に正しい位置（低いエリア）へ即座に移動できるようにする
                //     Vector3 targetPos = framing.FollowTargetPosition;
                //     targetPos.z = cam.transform.position.z;
                //     cam.transform.position = targetPos;

                //     yield return null; // 1フレーム待機（物理・Confiner等の更新）

                //     // 4. 最低待機時間を超えるまでは完了判定をスキップ
                //     if (timeElapsed < minDuration)
                //     {
                //         continue;
                //     }

                //     // 5. 判定
                //     Vector3 currentCamPos = cam.transform.position;
                //     Vector3 currentTargetPos = framing.FollowTargetPosition;

                //     float distanceXY = Vector2.Distance(
                //         new Vector2(currentCamPos.x, currentCamPos.y),
                //         new Vector2(currentTargetPos.x, currentTargetPos.y)
                //     );

                //     bool isCloseEnough = distanceXY <= 0.1f;

                //     // 端にいるかどうかの判定。
                //     // minDuration経過後であれば、Confinerは正しいものになっているはずなので、
                //     // ここで端判定が出れば「本当に端にいて動けない」と判断できる。
                //     bool isAtEdge = boundaryChecker.CameraAtEdge != null;

                //     // 静止判定（上下の壁に当たって動けないケースなどに対応）
                //     // 強制移動させているにも関わらず座標が変わらない＝Confiner等で止められていると判断
                //     bool isStationary = Vector3.Distance(currentCamPos, lastCamPos) < 0.001f;

                //     bool isTimeOut = timeElapsed >= timeOut;

                //     if (isCloseEnough || isAtEdge || isStationary || isTimeOut)
                //     {
                //         // タイムアウトかつ、静止もしていない（何かに引っかかって震えている等）場合のみ警告
                //         if (isTimeOut && !isStationary && !isCloseEnough)
                //         {
                //             Debug.LogWarning($"CameraMove Timeout (Dist:{distanceXY:F2}).");
                //         }
                //         break;
                //     }

                //     // 次フレーム比較用に座標を更新
                //     lastCamPos = currentCamPos;
                // }

                // // 6. Damping設定を元に戻す
                // framing.m_XDamping = prevXDamping;
                // framing.m_YDamping = prevYDamping;

                // Cinemachineにワープを通知（内部演算のリセット）
                virtualCamera.OnTargetObjectWarped(
                    framing.FollowTarget,
                    framing.FollowTargetPosition - cam.transform.position
                );

                // カメラの座標をターゲット位置へ強制的に移動させる
                Vector3 targetPos = framing.FollowTargetPosition;
                targetPos.z = cam.transform.position.z;
                cam.transform.position = targetPos;

                // 前フレームまでの位置計算や慣性をすべて破棄し、即座にカットさせる
                virtualCamera.PreviousStateIsValid = false;

                // 【重要】CameraMoveAreaのConfiner更新（最大10フレーム程度かかる）を待つため、
                // 最低でもこの時間は待機し、物理・Confiner等の更新を安定させる。
                // ワープの暗転中（FadeOut後）に行われるため、プレイヤーからは待機時間は見えません。
                yield return new WaitForSecondsRealtime(0.25f);
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
        /// 指定時間だけPerlinNoiseを有効化し、その後無効化します。
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
        /// 指定されたパラメータでPerlinNoiseを有効化し、時間経過後に停止します。
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
        /// Cinemachine Brainを一時的に無効化し、DOTweenを使用してカメラを振動させます。完了後、CameraResetを呼び出します。
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

        /// <summary>
        /// 外部コマンドから無期限（または指定時間）の持続的なカメラシェイクを開始します。
        /// </summary>
        public void PlayContinuousShake(float amplitude, float frequency)
        {
            PlayContinuousShake(amplitude, frequency, false, Vector2.zero);
        }

        /// <summary>
        /// 持続シェイクを開始し、停止後のカメラ座標を設定します。
        /// Cinemachine Brain停止中は実カメラを直接揺らします。
        /// </summary>
        /// <param name="amplitude">揺れの強さ（振幅）</param>
        /// <param name="frequency">揺れの速さ（周波数）</param>
        /// <param name="isUseCustomEndPosition">停止後に任意座標を使用するか</param>
        /// <param name="customEndPosition">停止後のX/Y座標。Z座標はシェイク開始時の値を維持します。</param>
        public void PlayContinuousShake(
            float amplitude,
            float frequency,
            bool isUseCustomEndPosition,
            Vector2 customEndPosition
        )
        {
            if (cam == null)
                return;

            // 既存のシェイク処理があれば強制停止
            if (shakeCoroutine != null)
                StopCoroutine(shakeCoroutine);
            continuousShakeTween?.Kill();

            IsContinuousShakeActive = true;
            isPriorityShakeActive = true; // 他のヒットストップ等による上書きを防止

            manualShakeStartPosition = cam.transform.localPosition;
            manualShakeEndPosition = isUseCustomEndPosition
                ? customEndPosition
                : new Vector2(manualShakeStartPosition.x, manualShakeStartPosition.y);

            var brain = cam.GetComponent<CinemachineBrain>();
            isManualContinuousShakeActive = brain != null && !brain.enabled;

            if (isManualContinuousShakeActive)
            {
                manualShakeAmplitude = amplitude;
                manualShakeFrequency = frequency;
                return;
            }

            if (perlinNoise == null)
            {
                IsContinuousShakeActive = false;
                isPriorityShakeActive = false;
                return;
            }

            perlinNoise.m_NoiseProfile = takeHitNoiseSettings; // ヒット時と同じProfileを使用
            perlinNoise.m_AmplitudeGain = amplitude;
            perlinNoise.m_FrequencyGain = frequency;
        }

        /// <summary>
        /// 実行中の持続的なカメラシェイクを停止します。
        /// </summary>
        /// <param name="fadeDuration">フェードアウトにかける時間（0で即座に停止）</param>
        public void StopContinuousShake(float fadeDuration)
        {
            // 実行中でなければ何もしない
            if (!IsContinuousShakeActive)
                return;

            IsContinuousShakeActive = false; // フラグを即座に折り、多重実行を防止
            continuousShakeTween?.Kill();

            if (isManualContinuousShakeActive)
            {
                if (fadeDuration <= 0f)
                {
                    CompleteManualContinuousShake();
                }
                else
                {
                    continuousShakeTween = DOTween
                        .To(
                            () => manualShakeAmplitude,
                            x => manualShakeAmplitude = x,
                            0f,
                            fadeDuration
                        )
                        .SetUpdate(true)
                        .OnComplete(CompleteManualContinuousShake);
                }
                return;
            }

            if (perlinNoise == null)
            {
                isPriorityShakeActive = false;
                return;
            }

            if (fadeDuration <= 0f)
            {
                // 即座に停止
                perlinNoise.m_AmplitudeGain = 0f;
                perlinNoise.m_FrequencyGain = 0f;
                perlinNoise.m_NoiseProfile = null;
                isPriorityShakeActive = false; // 優先フラグを解除して通常ヒット時の揺れを許可
            }
            else
            {
                // DOTweenを使って徐々に揺れ（Amplitude）を0に近づける
                continuousShakeTween = DOTween
                    .To(
                        () => perlinNoise.m_AmplitudeGain,
                        x => perlinNoise.m_AmplitudeGain = x,
                        0f,
                        fadeDuration
                    )
                    .SetUpdate(true) // TimeScale=0でもフェードアウトさせる
                    .OnComplete(() =>
                    {
                        perlinNoise.m_FrequencyGain = 0f;
                        perlinNoise.m_NoiseProfile = null;
                        isPriorityShakeActive = false;
                    });
            }
        }

        private void CompleteManualContinuousShake()
        {
            isManualContinuousShakeActive = false;
            manualShakeAmplitude = 0f;
            isPriorityShakeActive = false;

            if (cam == null)
                return;

            cam.transform.localPosition = new Vector3(
                manualShakeEndPosition.x,
                manualShakeEndPosition.y,
                manualShakeStartPosition.z
            );
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
                framing.m_YDamping = currentBaseYDamping;
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
        /// カメラのレンズ設定、追従オフセット、Dampingを変更します。
        /// </summary>
        /// <param name="orthoSize">ターゲットとなるOrthographic Size</param>
        /// <param name="nearClip">ターゲットとなるNear Clip Plane</param>
        /// <param name="offset">ターゲットとなるFollow Offset</param>
        /// <param name="damping">ターゲットとなるDamping (X, Y)</param>
        /// <param name="duration">変更にかける時間（秒）。0の場合は即時変更。</param>
        public void SetCameraSettings(
            float orthoSize,
            float nearClip,
            Vector3 offset,
            Vector2 damping,
            float duration = 1.0f
        )
        {
            if (virtualCamera == null || framing == null)
                return;

            // 既存のTweenがあればキルする
            lensTween?.Kill();
            offsetTween?.Kill();
            xDampingTween?.Kill();
            yDampingTween?.Kill();

            //  新しいDamping値を基準値として保存
            // これにより、他の処理でリセットがかかっても、この値に戻るようになる
            currentBaseXDamping = damping.x;
            currentBaseYDamping = damping.y;

            if (duration <= 0f)
            {
                // 即時変更
                // m_Lensは構造体のため、一度変数に受けてから変更し再代入する
                var lens = virtualCamera.m_Lens;
                lens.OrthographicSize = orthoSize;
                lens.NearClipPlane = nearClip;
                virtualCamera.m_Lens = lens;

                framing.m_FollowOffset = offset;
                framing.m_XDamping = damping.x;
                framing.m_YDamping = damping.y;
            }
            else
            {
                // DOTweenで滑らかに変更
                // m_Lensは構造体のため、直接のプロパティ変更が反映されない現象を防ぐため再代入方式でTweenする
                lensTween = DOTween
                    .To(
                        () => virtualCamera.m_Lens.OrthographicSize,
                        x =>
                        {
                            var lens = virtualCamera.m_Lens;
                            lens.OrthographicSize = x;
                            virtualCamera.m_Lens = lens;
                        },
                        orthoSize,
                        duration
                    )
                    .SetUpdate(true); // TimeScaleの影響を受けないようにする場合

                // NearClipは通常即時変更で問題ないが、確実に反映させるため再代入を行う
                var initialLens = virtualCamera.m_Lens;
                initialLens.NearClipPlane = nearClip;
                virtualCamera.m_Lens = initialLens;

                offsetTween = DOTween
                    .To(
                        () => framing.m_FollowOffset,
                        x => framing.m_FollowOffset = x,
                        offset,
                        duration
                    )
                    .SetUpdate(true);
                xDampingTween = DOTween
                    .To(() => framing.m_XDamping, x => framing.m_XDamping = x, damping.x, duration)
                    .SetUpdate(true);

                yDampingTween = DOTween
                    .To(() => framing.m_YDamping, x => framing.m_YDamping = x, damping.y, duration)
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
                new Vector2(
                    GameConstants.CAMERA_FOLLOW_DAMPING_X,
                    GameConstants.CAMERA_FOLLOW_DAMPING_Y
                ),
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
                // Timelineモード終了時の復帰処理

                // 1. 追尾対象の復元
                // Timeline中に追尾していたダミーオブジェクトから、元の対象（プレイヤー等）に戻す
                if (originalFollowTarget != null)
                {
                    virtualCamera.Follow = originalFollowTarget;
                }

                // 2. Confiner（移動制限）の一時解除
                // これを行わないと、Timelineの最終地点からプレイヤー位置へ戻る際、
                // 「前のエリアの壁」に衝突してカメラが戻れなくなる場合があるため、一旦無効化する。
                if (confiner != null)
                {
                    confiner.m_BoundingShape2D = null;
                }

                // 3. Damping（追従遅延）設定の復元
                // 通常プレイ用の滑らかな動きに戻すため、保存しておいた値を適用する。
                framing.m_XDamping = originalXDamping;
                framing.m_YDamping = originalYDamping;

                // 4. カメラ位置の強制ワープ（重要！）
                // PreviousStateIsValid を false にセットすることで、Cinemachineに
                // 「前フレームまでの位置計算や慣性をすべて破棄せよ」と命令する。
                // これにより、Damping設定（滑らかさ）を無視して、次のフレームで即座に
                // ターゲット（プレイヤー）の位置へカメラが「カット（瞬間移動）」する。
                // ※これをしないと、カメラが遠くからゆっくりプレイヤーへ戻ってきてしまう。
                virtualCamera.PreviousStateIsValid = false;

                // 5. 現在地のエリア情報の再適用
                // Confinerをnullにした状態なので、現在プレイヤーがいる場所の
                // CameraMoveArea（境界線やズーム設定など）を即座に再検索・適用させる。
                // これにより、正しいエリア制限がセットされた状態でゲームに復帰できる。
                CameraMoveArea.RefreshActiveArea();
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

        #region Area Lock Control

        /// <summary>
        /// カメラの追従対象をダミーオブジェクトに変更し、指定した座標（エリア中心）に完全に固定します。
        /// </summary>
        public void SetAreaCameraLock(bool isLocked, Vector2 targetPosition)
        {
            if (virtualCamera == null || framing == null || areaLockTargetObject == null)
                return;

            // Timelineモードがアクティブな場合は、演出を優先するためロック処理を行わない
            if (IsTimelineControlMode)
                return;

            isAreaLocked = isLocked;

            if (isLocked)
            {
                // 現在のターゲットがダミーオブジェクトでなければ、元のターゲット（プレイヤー）として保存
                if (
                    virtualCamera.Follow != areaLockTargetObject.transform
                    && virtualCamera.Follow != timelineTargetObject.transform
                )
                {
                    originalFollowTarget = virtualCamera.Follow;
                }

                // 追従対象をエリアロック用ダミーに切り替え
                virtualCamera.Follow = areaLockTargetObject.transform;

                // FollowOffset（カメラと対象の距離設定）の影響を逆算して打ち消し、
                // 画面の中央がピタリと targetPosition に合うようにダミーの位置を調整する
                Vector3 offset = framing.m_FollowOffset;
                areaLockTargetObject.transform.position = new Vector3(
                    targetPosition.x - offset.x,
                    targetPosition.y - offset.y,
                    cam.transform.position.z
                );
            }
            else
            {
                // ロック解除時、Timelineモードでなければ元のターゲット（プレイヤー）に戻す
                if (originalFollowTarget != null && !IsTimelineControlMode)
                {
                    virtualCamera.Follow = originalFollowTarget;
                }

                // ※ ここに virtualCamera.PreviousStateIsValid = false; を入れると
                // ロック解除時に一瞬でプレイヤーにカメラがワープして戻ります。
                // ボス部屋の解除などで「カメラをゆっくりプレイヤーに戻したい」場合は不要なため省いています。
            }
        }

        #endregion
    }
}
