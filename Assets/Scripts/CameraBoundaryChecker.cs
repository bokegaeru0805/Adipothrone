using System.Collections;
using Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CameraBoundaryChecker : MonoBehaviour
{
    [Tooltip("カメラが端にいるかどうかを他スクリプトから参照可能にします")]
    public string CameraAtEdge { get; private set; } = null;
    private float margin = 0.1f;
    private CinemachineConfiner2D confiner;
    private CinemachineBrain cinemachineBrain;

    private void Start()
    {
        confiner = Camera.main.GetComponent<CinemachineConfiner2D>();
        CameraAtEdge = null;

        CinemachineVirtualCamera virtualCamera =
            Camera.main.GetComponent<CinemachineVirtualCamera>();
        if (virtualCamera == null)
        {
            Debug.LogError("CinemachineVirtualCameraが見つかりません。");
            return;
        }
        else
        {
            virtualCamera.enabled = true; // Virtual Cameraを有効化
        }

        // このスクリプトがアタッチされているカメラにCinemachineBrainがあるかを探す
        cinemachineBrain = Camera.main.GetComponent<CinemachineBrain>();

        if (cinemachineBrain == null)
        {
            Debug.LogError("このカメラにCinemachineBrainコンポーネントが見つかりません。");
            return;
        }

        // 初期化時にFollowOffsetを設定
        SetCinemachineFollowOffset(GameConstants.PLAYER_CAMERA_FOLLOW_OFFSET);
    }

    private void OnEnable()
    {
        // シーンが切り替わった時に OnSceneChanged メソッドを呼び出すように登録
        SceneManager.activeSceneChanged += OnSceneChanged;
    }

    private void OnDisable()
    {
        // オブジェクトが破棄される際に、登録を解除（メモリリーク防止）
        SceneManager.activeSceneChanged -= OnSceneChanged;
    }

    // 外部からFollowOffsetを変更するための公開メソッド
    public void SetCinemachineFollowOffset(Vector3 newOffset)
    {
        // 現在アクティブなVirtual Cameraを取得
        CinemachineVirtualCamera activeVCam = Camera.main.GetComponent<CinemachineVirtualCamera>();

        if (activeVCam != null)
        {
            // Bodyのコンポーネントを取得
            // ここではCinemachineTransposerを想定していますが、
            // 別のBodyタイプを使っている場合は適宜変更してください。
            CinemachineTransposer transposer =
                activeVCam.GetCinemachineComponent<CinemachineTransposer>();

            if (transposer != null)
            {
                // FollowOffsetを設定
                transposer.m_FollowOffset = newOffset;
            }
            else
            {
                Debug.LogWarning(
                    "アクティブなVirtual CameraのBodyにCinemachineTransposerが見つかりません。"
                );
                // 他のBodyタイプも考慮する場合の例
                // CinemachineFramingTransposer framingTransposer = activeVCam.GetCinemachineComponent<CinemachineFramingTransposer>();
                // if (framingTransposer != null) { framingTransposer.m_TrackedObjectOffset = newOffset; } // FramingTransposerはTrackedObjectOffsetを使う場合が多い
            }
        }
        else
        {
            Debug.LogWarning("現在アクティブなCinemachine Virtual Cameraがありません。");
        }
    }

    private void Update()
    {
        if (confiner == null)
            return;

        var boundingShape = confiner.m_BoundingShape2D;
        if (boundingShape == null)
            return;

        Bounds bounds = boundingShape.bounds;
        // 画面左下 (0,0)
        Vector3 bottomLeft = Camera.main.ViewportToWorldPoint(new Vector3(0, 0, 0));

        // 画面右上 (1,1)
        Vector3 topRight = Camera.main.ViewportToWorldPoint(new Vector3(1, 1, 0));

        float left = bottomLeft.x;
        float right = topRight.x;
        //float bottom = bottomLeft.y;
        //float top = topRight.y;

        bool atLeftEdge = left <= bounds.min.x + margin;
        bool atRightEdge = right >= bounds.max.x - margin;

        // 全体判定（どこかの端にいる）
        if (atLeftEdge)
        {
            CameraAtEdge = "left";
        }
        else if (atRightEdge)
        {
            CameraAtEdge = "right";
        }
        else
        {
            CameraAtEdge = null;
        }
    }

    /// <summary>
    /// シーンが変更されたときに呼び出される
    /// </summary>
    private void OnSceneChanged(Scene current, Scene next)
    {
        //ゲームがまだ開始されていない場合は何もしない
        if (!GameManager.isFirstGameSceneOpen)
            return;

        ForceCameraPosition(new Vector2(0, 1000));
    }

    /// <summary>
    /// Cinemachineを一時的に無効化し、カメラを指定した座標へ強制的に移動させます。
    /// シーン切り替え直後などに外部のスクリプトから呼び出して使用します。
    /// </summary>
    /// <param name="targetPosition">移動先の座標 (X, Y)</param>
    public void ForceCameraPosition(Vector2 targetPosition)
    {
        // コルーチンを開始して、フレームをまたいだ安全な座標設定を行う
        StartCoroutine(ForcePositionCoroutine(targetPosition));
    }

    private IEnumerator ForcePositionCoroutine(Vector2 targetPosition)
    {
        // 1. Cinemachine Brainを無効化して、手動制御に切り替える
        if (cinemachineBrain != null)
        {
            cinemachineBrain.enabled = false;
        }

        // 2. フレームの最後に処理を遅らせ、Brainの無効化を確実にする
        yield return new WaitForEndOfFrame();

        // 3. カメラの座標を強制的に設定する（Z座標は現在の値を維持）
        Vector3 newPos = new Vector3(targetPosition.x, targetPosition.y, transform.position.z);
        transform.position = newPos;

        // 4. 次のフレームでCinemachine Brainを再度有効化し、制御を戻す
        //    (すぐに有効化すると、Cinemachineが古い位置にカメラを戻そうとすることがあるため)
        yield return null;

        if (cinemachineBrain != null)
        {
            cinemachineBrain.enabled = true;
        }
    }
}
