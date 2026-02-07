using System.Collections;
using Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// CinemachineConfiner2Dの境界に基づいて、カメラが画面端に到達しているかを判定するクラス。
/// また、シーン遷移時などにカメラ位置を強制移動させる機能も提供します。
/// </summary>
public class CameraBoundaryChecker : MonoBehaviour
{
    #region Public Properties

    /// <summary>
    /// カメラが端にいるかどうかを示すステータス文字列。
    /// "left": 左端, "right": 右端, null: 端ではない
    /// </summary>
    [field: Tooltip("カメラが端にいるかどうかを他スクリプトから参照可能にします")]
    public string CameraAtEdge { get; private set; } = null;

    #endregion

    #region Private Fields

    [Header("判定設定")]
    [Tooltip("境界判定の遊び（マージン）。この値の分だけ内側でも端とみなします。")]
    [SerializeField]
    private float margin = 0.1f;

    // 内部コンポーネント参照
    private CinemachineConfiner2D confiner;
    private CinemachineBrain cinemachineBrain;

    #endregion

    #region Unity Lifecycle Methods

    private void Start()
    {
        InitializeComponents();
    }

    private void OnEnable()
    {
        // シーン遷移イベントの購読
        SceneManager.activeSceneChanged += OnSceneChanged;
    }

    private void OnDisable()
    {
        // イベント購読の解除
        SceneManager.activeSceneChanged -= OnSceneChanged;
    }

    private void Update()
    {
        CheckBoundaryStatus();
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// 外部からCinemachine Virtual CameraのFollow Offsetを変更します。
    /// </summary>
    /// <param name="newOffset">新しいオフセット値</param>
    public void SetCinemachineFollowOffset(Vector3 newOffset)
    {
        // 現在アクティブなVirtual Cameraを取得
        // ※ Camera.mainと同じオブジェクトにVCamがアタッチされている構成を前提としています
        var activeVCam = Camera.main.GetComponent<CinemachineVirtualCamera>();

        if (activeVCam != null)
        {
            // Transposerコンポーネント（Body）を取得してオフセットを設定
            var transposer = activeVCam.GetCinemachineComponent<CinemachineTransposer>();

            if (transposer != null)
            {
                transposer.m_FollowOffset = newOffset;
            }
            else
            {
                Debug.LogWarning("CameraBoundaryChecker: アクティブなVirtual CameraにCinemachineTransposerが見つかりません。");
                // FramingTransposerを使用している場合は以下のように対応可能です
                // var framingTransposer = activeVCam.GetCinemachineComponent<CinemachineFramingTransposer>();
                // if (framingTransposer != null) framingTransposer.m_TrackedObjectOffset = newOffset;
            }
        }
        else
        {
            Debug.LogWarning("CameraBoundaryChecker: メインカメラにCinemachineVirtualCameraコンポーネントが見つかりません。");
        }
    }

    /// <summary>
    /// Cinemachineを一時的に無効化し、カメラを指定した座標へ強制的に移動させます。
    /// シーン切り替え直後などに有効です。
    /// </summary>
    /// <param name="targetPosition">移動先の座標 (X, Y)</param>
    public void ForceCameraPosition(Vector2 targetPosition)
    {
        StartCoroutine(ForcePositionCoroutine(targetPosition));
    }

    #endregion

    #region Internal Logic & Coroutines

    /// <summary>
    /// 必要なコンポーネントの取得と初期設定
    /// </summary>
    private void InitializeComponents()
    {
        confiner = Camera.main.GetComponent<CinemachineConfiner2D>();

        // Virtual Cameraのチェックと有効化
        var virtualCamera = Camera.main.GetComponent<CinemachineVirtualCamera>();
        if (virtualCamera == null)
        {
            Debug.LogError("CameraBoundaryChecker: CinemachineVirtualCameraが見つかりません。", this);
        }
        else
        {
            virtualCamera.enabled = true;
        }

        // CinemachineBrainの取得
        cinemachineBrain = Camera.main.GetComponent<CinemachineBrain>();
        if (cinemachineBrain == null)
        {
            Debug.LogError("CameraBoundaryChecker: CinemachineBrainコンポーネントが見つかりません。", this);
            return;
        }

        // 初期FollowOffsetの設定
        SetCinemachineFollowOffset(GameConstants.PLAYER_CAMERA_FOLLOW_OFFSET);
    }

    /// <summary>
    /// カメラが境界の端にいるかを判定し、CameraAtEdgeプロパティを更新する
    /// </summary>
    private void CheckBoundaryStatus()
    {
        if (confiner == null || confiner.m_BoundingShape2D == null)
        {
            CameraAtEdge = null;
            return;
        }

        Bounds bounds = confiner.m_BoundingShape2D.bounds;

        // ビューポート座標(0,0)=左下, (1,1)=右上 をワールド座標に変換して、カメラの映している範囲を取得
        Vector3 bottomLeft = Camera.main.ViewportToWorldPoint(new Vector3(0, 0, 0));
        Vector3 topRight = Camera.main.ViewportToWorldPoint(new Vector3(1, 1, 0));

        float left = bottomLeft.x;
        float right = topRight.x;

        // マージンを含めて判定
        bool atLeftEdge = left <= bounds.min.x + margin;
        bool atRightEdge = right >= bounds.max.x - margin;

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
    /// シーン変更時のコールバック
    /// </summary>
    private void OnSceneChanged(Scene current, Scene next)
    {
        // ゲーム初回起動時以外で実行（初回ロード時は配置済みの場所を使うため）
        if (!GameManager.isFirstGameSceneOpen)
            return;

        // シーン遷移時はカメラを安全な場所（空の彼方など）へ一時退避させる
        ForceCameraPosition(new Vector2(0, 1000));
    }

    /// <summary>
    /// Cinemachineの制御を一時停止して座標を強制変更するコルーチン
    /// </summary>
    private IEnumerator ForcePositionCoroutine(Vector2 targetPosition)
    {
        // 1. Brainを無効化（Cinemachineによる座標上書きを防ぐ）
        if (cinemachineBrain != null)
        {
            cinemachineBrain.enabled = false;
        }

        // 2. 現在のフレーム描画が終わるまで待機
        yield return new WaitForEndOfFrame();

        // 3. 座標を強制適用 (Z座標は維持)
        Vector3 newPos = new Vector3(targetPosition.x, targetPosition.y, transform.position.z);
        transform.position = newPos;

        // 4. 1フレーム待ってからBrainを再有効化
        // 即座に戻すと、Cinemachineがまだ古いターゲット位置を計算してしまい、
        // カメラが一瞬元の位置に戻るチラつきが発生するのを防ぐ
        yield return null;

        if (cinemachineBrain != null)
        {
            cinemachineBrain.enabled = true;
        }
    }

    #endregion
}