using System.Collections;
using Cinemachine;
using DG.Tweening;
using UnityEngine;

namespace MyGame.CameraControl
{
    public class CameraManager : MonoBehaviour
    {
        public static CameraManager instance { get; private set; }
        private Camera cam;
        private CinemachineVirtualCamera virtualCamera;
        private CinemachineTransposer framing;
        private CameraBoundaryChecker boundaryChecker;

        // 実行中のダンピングリセットコルーチンを管理するための変数
        private Coroutine dampingResetCoroutine = null;

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;

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

                if (boundaryChecker == null)
                {
                    Debug.LogError("CameraManagerはCameraBoundaryCheckerを取得できませんでした");
                }

                // 自動でCinemachineVirtualCameraを取得
                if (virtualCamera == null)
                {
                    virtualCamera = FindObjectOfType<CinemachineVirtualCamera>();
                }

                if (cam == null || virtualCamera == null)
                {
                    Debug.LogError("CameraManagerはカメラに関する要素を取得できませんでした");
                    return;
                }
                else
                {
                    virtualCamera.enabled = false;
                    // Virtual Cameraを初期状態では無効化
                    //CameraBoundaryCheckerで有効化される
                }

                // CinemachineTransposerを取得
                framing = virtualCamera.GetCinemachineComponent<CinemachineTransposer>();
                if (framing != null)
                {
                    framing.m_YDamping = GameConstants.CameraFollowDampingY; // 初期のYDamping値を設定
                }
                else
                {
                    Debug.LogError("CameraManagerはCinemachineTransposerを取得できませんでした");
                }
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public IEnumerator CameraMove()
        {
            if (framing != null)
            {
                // YDampingを0にして即座にプレイヤー位置に追従させる
                framing.m_YDamping = 0;
                yield return null; // 1フレーム待ってCinemachineが位置を更新するのを待つ

                while (true) // ループ自体は常にtrueにし、中のbreakで抜ける
                {
                    Vector3 cameraPos = Camera.main.transform.position;
                    Vector3 targetPos = framing.FollowTargetPosition;

                    // 条件1：カメラとターゲットの距離が閾値以下になったらループを抜ける（元の条件）
                    bool isCloseEnough =
                        Vector3.Distance(cameraPos, targetPos)
                        <= GameConstants.PLAYER_CAMERA_FOLLOW_OFFSET.magnitude + 0.1f;

                    // 条件2：カメラが移動範囲の端におり、かつX座標の差が閾値以下になったらループを抜ける
                    bool isAtEdge = boundaryChecker.CameraAtEdge != null;

                    if (isCloseEnough || isAtEdge)
                    {
                        break; // どちらかの条件を満たしたら待機を終了
                    }

                    yield return null; // 条件を満たさない場合は1フレーム待つ
                }

                framing.m_YDamping = GameConstants.CameraFollowDampingY; // 元のYDamping値に戻す
            }
            else
            {
                Debug.LogError("CinemachineTransposerが見つかりません。カメラの追従ができません。");
            }
        }

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

        public void StartCameraShake(Vector3 positionStrength, float shakeDuration)
        {
            StartCoroutine(CameraShake(positionStrength, shakeDuration));
        }

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

        public void CameraReset()
        {
            Camera.main.GetComponent<CinemachineBrain>().enabled = true;
        }

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

        private IEnumerator TemporaryResetYDampingCoroutine(float duration)
        {
            if (framing != null)
            {
                // YDampingを0にして即座に追従させる
                framing.m_YDamping = 0;

                // 指定された時間だけ待つ
                yield return new WaitForSecondsRealtime(duration);

                // 元のYDamping値に戻す
                framing.m_YDamping = GameConstants.CameraFollowDampingY;
            }
            else
            {
                Debug.LogError("CinemachineTransposerが見つかりません。");
            }

            // コルーチンの管理変数をクリア
            dampingResetCoroutine = null;
        }
    }
}
