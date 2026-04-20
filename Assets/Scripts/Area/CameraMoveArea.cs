using System;
using System.Collections;
using System.Collections.Generic;
using Cinemachine;
using MyGame.CameraControl;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine.SceneManagement;
#endif

/// <summary>
/// プレイヤーが特定のエリアに入るとカメラの境界、Volume Profile、2Dライトの形状を設定します。
/// エリア外に出ると元に戻します。
/// </summary>
[RequireComponent(typeof(CompositeCollider2D))]
public class CameraMoveArea : MonoBehaviour
{
    #region Static Members & Events

    /// <summary>
    /// 現在プレイヤーがいる、アクティブなCameraMoveAreaのインスタンス。
    /// </summary>
    private static CameraMoveArea activeArea = null;

    /// <summary>
    /// プレイヤーが、いずれかのCameraMoveAreaに入ったときに発行されるイベント。
    /// </summary>
    public static event Action<CameraMoveArea> OnPlayerEnteredArea;

    /// <summary>
    /// プレイヤーが、アクティブだったCameraMoveAreaから出たときに発行されるイベント。
    /// </summary>
    public static event Action<CameraMoveArea> OnPlayerExitedArea;

    /// <summary>
    /// 現在アクティブなカメラ移動エリアの境界（Bounds）をワールド座標で取得します。
    /// アクティブなエリアがない場合はnullを返します。
    /// </summary>
    public static Bounds? ActiveAreaBounds
    {
        get
        {
            if (activeArea != null && activeArea.areaCollider != null)
            {
                return activeArea.areaCollider.bounds;
            }
            return null;
        }
    }

    /// <summary>
    /// エリア進入時の自動BGM再生をロックするかどうかのフラグ。
    /// イベント中などに別のBGMで上書きされるのを防ぐために使用します。
    /// </summary>
    private static bool isAreaBgmLocked = false;

    /// <summary>
    /// ドメインリロードが無効な場合や、シーン遷移時に静的変数が残るのを防ぐためのリセット処理
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        activeArea = null;
        isAreaBgmLocked = false;
    }

    /// <summary>
    /// ロードによるシーン遷移が行われた際、強制的に静的フラグや参照をリセットします。
    /// これにより、前のシーンでのBGMロック状態やエリア判定が次のシーンに持ち越されるバグを防ぎます。
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneChangeCallback()
    {
        // エディタのドメインリロード無効時などでの多重登録を防ぐため、一度解除してから登録する
        UnityEngine.SceneManagement.SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        UnityEngine.SceneManagement.SceneManager.activeSceneChanged += OnActiveSceneChanged;
    }

    private static void OnActiveSceneChanged(
        UnityEngine.SceneManagement.Scene current,
        UnityEngine.SceneManagement.Scene next
    )
    {
        activeArea = null;
        isAreaBgmLocked = false;
    }

    /// <summary>
    /// 現在プレイヤーがいるアクティブなエリアのBGMをフェードインで再生します。
    /// </summary>
    public static void PlayCurrentAreaBgm(float fadeDuration)
    {
        if (BGMManager.instance == null || activeArea == null)
        {
            Debug.LogWarning(
                "BGMManagerまたはアクティブなエリアが見つからないため、BGMを再生できません。"
            );
            return;
        }

        BGMCategory bgmToPlay = activeArea.GetBgmForCurrentFlags();
        BGMManager.instance.FadeIn(bgmToPlay, fadeDuration);
    }

    #endregion

    #region Inspector Settings

    [Header("追従する背景オブジェクト")]
    [Tooltip("カメラに追従して動かしたい背景のGameObject")]
    [SerializeField]
    private GameObject backGround;

    [Header("ポストプロセス設定")]
    [Tooltip("このエリア内に入ったときに適用するVolume Profile")]
    [SerializeField]
    private VolumeProfile areaVolumeProfile;

    [Header("プレイヤーの影設定")]
    [Tooltip("このエリア内でプレイヤーの足元に影を表示するかどうか")]
    [SerializeField]
    private bool enablePlayerShadow = false;

    /// <summary>
    /// 外部から影の有効状態を読み取るためのプロパティ
    /// </summary>
    public bool EnablePlayerShadow => enablePlayerShadow;

    [Header("2Dライト設定")]
    [Tooltip("形状をこのエリアのコライダーに合わせたいFreeform Light 2D")]
    [SerializeField]
    private Light2D areaLight;

    [Header("BGM設定")]
    [Tooltip("どのフラグ条件にも一致しない場合に再生される、デフォルトのBGM")]
    [SerializeField]
    private BGMCategory defaultBgm;

    [InfoBox("時系列が後の条件（進行度が高いもの）を下に配置してください。")]
    [Tooltip(
        "特定のフラグが立っている場合に、優先的に再生するBGMのリスト。下から順（逆順）に評価され、最初に一致したものが再生されます。"
    )]
    [SerializeField]
    private List<ConditionalBgm> conditionalBgms = new List<ConditionalBgm>();

    [Header("カメラ個別設定")]
    [Tooltip("このエリアに入った時、カメラのサイズやオフセットを変更するかどうか")]
    [SerializeField]
    private bool overrideCameraSettings = false;

    [Tooltip("変更後のOrthographic Size（ズーム具合）。無効時はデフォルト値が使用されます。")]
    [SerializeField, ShowIf(nameof(overrideCameraSettings))]
    private float targetOrthoSize = GameConstants.DEFAULT_CAMERA_ORTHO_SIZE;

    [Tooltip("変更後のNear Clip Plane。")]
    [SerializeField, ShowIf(nameof(overrideCameraSettings))]
    private float targetNearClip = GameConstants.DEFAULT_CAMERA_NEAR_CLIP;

    [Tooltip("変更後のFollow Offset（プレイヤーからの距離）。")]
    [SerializeField, ShowIf(nameof(overrideCameraSettings))]
    private Vector3 targetFollowOffset = new Vector3(0f, 4.5f, -10f);

    [Tooltip("変更後のDamping（カメラの追従遅延）。Xは横方向、Yは縦方向。(0で遅延なし)")]
    [SerializeField, ShowIf(nameof(overrideCameraSettings))]
    private Vector2 targetDamping = new Vector2(
        GameConstants.CAMERA_FOLLOW_DAMPING_X,
        GameConstants.CAMERA_FOLLOW_DAMPING_Y
    );

    [Tooltip("設定変更にかける時間（秒）")]
    [SerializeField, ShowIf(nameof(overrideCameraSettings))]
    private float settingsTransitionDuration = 0f;

    #endregion

    #region Internal State & References

    // コンポーネント参照
    private CompositeCollider2D areaCollider;
    private Transform playerTransform;

    // カメラ制御用
    private float cameraOffsetY;
    private float cameraHalfWidth;
    private float yDampingResetDuration = 0.2f;

    // 背景制御用
    private Coroutine backgroundMoveCoroutine = null;
    private Vector2 defaultBackgroundPosition = Vector2.zero;

    // 状態フラグ
    private bool isPlayerInArea = false;

#if UNITY_EDITOR
    private bool isDebugScene = false;
#endif

    #endregion

    #region Unity Lifecycle Methods

    private void Awake()
    {
        InitializeComponents();

        if (backGround != null)
        {
            defaultBackgroundPosition = backGround.transform.position;
            backGround.SetActive(false);
        }

        if (areaLight != null)
        {
            areaLight.gameObject.SetActive(false);
        }

#if UNITY_EDITOR
        isDebugScene = SceneManager.GetActiveScene().name.Contains("Debug");
#endif
    }

    private void OnValidate()
    {
        if (areaCollider == null)
        {
            areaCollider = GetComponent<CompositeCollider2D>();
        }
        UpdateLightShapeToCollider();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag(GameConstants.PLAYER_TAG_NAME))
        {
            // 多重実行防止
            if (activeArea == this)
                return;

            // 他のエリアからの遷移処理
            if (activeArea != null && activeArea != this)
            {
                activeArea.HandlePlayerExit();
            }

            HandlePlayerEnter(other);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag(GameConstants.PLAYER_TAG_NAME))
        {
            // エリア内にいる判定だけはオフにする
            isPlayerInArea = false;

            // 【重要】自分が現在アクティブなエリアだった場合でも、ここでは退出処理を行いません。
            // プレイヤーがマージン（あそび）の隙間にいる間は、背景やカメラ制限、Lightを維持するためです。
            // 実際に HandlePlayerExit() が呼ばれてLight等が消えるのは、
            // 「次の新しいエリアの OnTriggerEnter2D に入った瞬間」になります。
        }
    }

    #endregion

    #region Core Logic (Enter / Exit)

    /// <summary>
    /// コンポーネントの取得と初期化
    /// </summary>
    private void InitializeComponents()
    {
        areaCollider = GetComponent<CompositeCollider2D>();

        UpdateLightShapeToCollider();

        // Main Cameraチェック
        if (Camera.main == null)
        {
            Debug.LogError("メインカメラが見つかりません。", this);
            return;
        }

        cameraHalfWidth = Camera.main.orthographicSize * Camera.main.aspect;
    }

    /// <summary>
    /// プレイヤーがエリアに入った際のメイン処理
    /// </summary>
    private void HandlePlayerEnter(Collider2D playerCollider)
    {
        activeArea = this;
        isPlayerInArea = true;

        // プレイヤー情報のキャッシュ
        playerTransform = playerCollider.transform;
        cameraOffsetY = GameConstants.PLAYER_CAMERA_FOLLOW_OFFSET.y;

        // カメラDampingのリセット（カメラが遅れて壁にめり込むのを防ぐ）
        if (yDampingResetDuration > 0 && CameraManager.instance != null)
        {
            CameraManager.instance.TriggerTemporaryDampingReset(yDampingResetDuration);
        }

        // BGMロックがかかっていなければBGMを再生する
        if (!isAreaBgmLocked)
        {
            PlayBgmBasedOnFlags();
        }

        // Light有効化
        if (areaLight != null)
            areaLight.gameObject.SetActive(true);

        // 各種設定の適用
        ApplyAreaSettings();

        // 背景移動開始
        if (backgroundMoveCoroutine == null)
        {
            backgroundMoveCoroutine = StartCoroutine(MoveBackgroundWithCamera());
        }

        // イベント発行
        OnPlayerEnteredArea?.Invoke(this);
    }

    /// <summary>
    /// エリア設定（Volume, Camera, Confiner）を一括適用する
    /// </summary>
    private void ApplyAreaSettings()
    {
        // 1. Volume Profile
        if (GlobalVolumeManager.instance != null && areaVolumeProfile != null)
        {
            GlobalVolumeManager.instance.ChangeProfileImmediate(areaVolumeProfile);
        }

        // 2. Camera Settings
        if (overrideCameraSettings && CameraManager.instance != null)
        {
            CameraManager.instance.SetCameraSettings(
                targetOrthoSize,
                targetNearClip,
                targetFollowOffset,
                targetDamping,
                settingsTransitionDuration
            );
        }

        // 3. Cinemachine Confiner
        StartCoroutine(SetBoundingShape());
    }

    /// <summary>
    /// プレイヤーがエリアから出た際の処理
    /// </summary>
    private void HandlePlayerExit()
    {
        isPlayerInArea = false;

        // Light無効化
        if (areaLight != null)
            areaLight.gameObject.SetActive(false);

        // 背景処理停止
        if (backgroundMoveCoroutine != null)
        {
            StopCoroutine(backgroundMoveCoroutine);
            backgroundMoveCoroutine = null;
        }

        // 背景リセット
        if (backGround != null)
        {
            backGround.transform.position = defaultBackgroundPosition;
            backGround.SetActive(false);
        }

        // カメラ設定リセット
        if (overrideCameraSettings && CameraManager.instance != null)
        {
            CameraManager.instance.ResetCameraSettings(settingsTransitionDuration);
        }

        // イベント発行
        OnPlayerExitedArea?.Invoke(this);
    }

    #endregion

    #region Public Static Methods

    /// <summary>
    /// シーン内の全てのCameraMoveAreaを走査し、プレイヤーが現在いるエリアを強制的にアクティブにします。
    /// セーブロード時やファストトラベル後の初期化に使用してください。
    /// </summary>
    public static void RefreshActiveArea()
    {
        // プレイヤーの取得
        GameObject player = GameObject.FindGameObjectWithTag(GameConstants.PLAYER_TAG_NAME);
        if (player == null)
            return;

        Collider2D playerCollider = player.GetComponent<Collider2D>();
        if (playerCollider == null)
            return;

        Vector2 playerPos = player.transform.position;

        // シーン上の全てのアクティブなエリアを取得
        // (重い処理なので毎フレーム呼ぶのはNGですが、ロード時1回なら問題ありません)
        CameraMoveArea[] allAreas = FindObjectsOfType<CameraMoveArea>();

        foreach (var area in allAreas)
        {
            // エリアのコライダーを取得
            if (area.areaCollider == null)
                area.areaCollider = area.GetComponent<CompositeCollider2D>();

            if (area.areaCollider == null)
                continue;

            // プレイヤーの座標がエリア内にあるか判定
            if (area.areaCollider.OverlapPoint(playerPos))
            {
                // エリア内なら強制的にEnter処理を実行
                // (内部で activeArea == this のチェックがあるため、二重実行は防がれます)
                area.HandlePlayerEnter(playerCollider);

                // 1つのエリアに入ったら終了（エリアが重なっていない前提）
                return;
            }

            // どのエリアにも入っていない（かつワープ等で強制リフレッシュされた）場合、
            // 古いアクティブエリアが残っていればここで破棄する
            if (activeArea != null)
            {
                activeArea.HandlePlayerExit();
                activeArea = null;
            }
        }
    }

    /// <summary>
    /// エリア進入時の自動BGM切り替えをロック/解除します。
    /// イベント中などでBGMを固定したい場合に true に設定します。
    /// false に設定してロックを解除した際、自動的に現在のエリアのBGMを再生し直します。
    /// </summary>
    /// <param name="isLocked">trueでロック、falseで解除</param>
    /// <param name="fadeDuration">ロック解除時にBGMを再生し直す際のフェード時間</param>
    public static void SetAreaBgmLocked(bool isLocked, float fadeDuration = 1.0f)
    {
        isAreaBgmLocked = isLocked;

        // ロックが解除されたら、現在のエリアの正しいBGMを流し直す
        if (!isLocked && activeArea != null)
        {
            PlayCurrentAreaBgm(fadeDuration);
        }
    }

    #endregion

    #region BGM Logic

    private void PlayBgmBasedOnFlags()
    {
        if (BGMManager.instance == null)
            return;

        BGMCategory bgmToPlay = GetBgmForCurrentFlags();
        BGMManager.instance.Play(bgmToPlay);
    }

    /// <summary>
    /// 現在のフラグ状況に基づいて、再生すべきBGMカテゴリを返します。
    /// </summary>
    public BGMCategory GetBgmForCurrentFlags()
    {
        // 条件リストを下から順（新しい/進行度が高い条件）に評価
        for (int i = conditionalBgms.Count - 1; i >= 0; i--)
        {
            var condition = conditionalBgms[i];
            if (condition.AreConditionsMet())
            {
                return condition.bgmToPlay;
            }
        }
        return defaultBgm;
    }

    #endregion

    #region Timeline Support

    /// <summary>
    /// Timelineなどから強制的にこのエリアをアクティブにします。
    /// カメラだけが移動し、プレイヤーが移動しない場合に使用します。
    /// </summary>
    public void ActivateFromTimeline()
    {
        if (activeArea == this)
            return;

        if (activeArea != null)
        {
            activeArea.HandlePlayerExit();
        }

        activeArea = this;

        // 簡易的な進入処理（イベント発行などは省略）
        if (areaLight != null)
            areaLight.gameObject.SetActive(true);

        ApplyAreaSettings();

        if (backGround != null)
        {
            backGround.SetActive(true);
            if (backgroundMoveCoroutine == null)
            {
                backgroundMoveCoroutine = StartCoroutine(MoveBackgroundWithCamera());
            }
        }
    }

    #endregion

    #region Helper Methods & Coroutines

    /// <summary>
    /// areaLightの形状をareaColliderの形状に合わせる
    /// </summary>
    public void UpdateLightShapeToCollider()
    {
        // if (areaLight != null)
        //     areaLight.gameObject.SetActive(true);

        if (areaLight == null || areaCollider == null)
            return;

        // Global Lightの場合は形状を持たないためスキップ
        if (areaLight.lightType == Light2D.LightType.Global)
            return;

        if (areaCollider.pathCount > 0)
        {
            Vector2[] pathPoints = new Vector2[areaCollider.GetPathPointCount(0)];
            areaCollider.GetPath(0, pathPoints);

            Vector3[] lightPath = new Vector3[pathPoints.Length];
            for (int i = 0; i < pathPoints.Length; i++)
            {
                Vector3 worldPoint = transform.TransformPoint(pathPoints[i]);
                lightPath[i] = areaLight.transform.InverseTransformPoint(worldPoint);
            }

            areaLight.SetShapePath(lightPath);
        }
    }

    /// <summary>
    /// CinemachineConfiner2Dの境界をこのエリアのColliderに設定する
    /// </summary>
    private IEnumerator SetBoundingShape()
    {
        CinemachineConfiner2D confiner = Camera.main.GetComponent<CinemachineConfiner2D>();

        if (confiner == null)
        {
#if UNITY_EDITOR
            if (!isDebugScene)
                Debug.LogWarning("メインカメラにCinemachineConfiner2Dが見つかりません。", this);
#endif
            yield break;
        }

        // 成功するまで数回試行（初期化タイミング対策）
        for (int i = 0; i < 10; i++)
        {
            confiner.m_BoundingShape2D = areaCollider;

            if (confiner.m_BoundingShape2D != null)
                yield break;

            yield return null;
        }

        Debug.LogWarning("CinemachineConfiner2DのBounding Shape設定に失敗しました。");
    }

    /// <summary>
    /// カメラの位置に基づいて背景を追従移動させる
    /// </summary>
    private IEnumerator MoveBackgroundWithCamera()
    {
        while (isPlayerInArea || activeArea == this)
        {
            if (backGround != null)
            {
                if (!backGround.activeSelf)
                    backGround.SetActive(true);

                Vector3 cameraPosition = Camera.main.transform.position;
                Vector3 playerPosition =
                    (playerTransform != null) ? playerTransform.position : Vector3.zero;

                bool isBrainEnabled = Camera.main.GetComponent<CinemachineBrain>().enabled;
                bool isTimelineMode = (
                    CameraManager.instance != null && CameraManager.instance.IsTimelineControlMode
                );

                // 通常プレイ時はプレイヤー基準、Timeline時はカメラ基準
                if (isBrainEnabled && !isTimelineMode)
                {
                    string cameraAtEdge = Camera
                        .main.GetComponent<CameraBoundaryChecker>()
                        .CameraAtEdge;

                    if (playerTransform != null)
                    {
                        Vector3 adjustedPlayerPos = playerPosition;
                        adjustedPlayerPos.y += cameraOffsetY;

                        // エリア端での補正
                        if (cameraAtEdge == "left")
                            adjustedPlayerPos.x = areaCollider.bounds.min.x + cameraHalfWidth;
                        else if (cameraAtEdge == "right")
                            adjustedPlayerPos.x = areaCollider.bounds.max.x - cameraHalfWidth;

                        backGround.transform.position = new Vector2(
                            adjustedPlayerPos.x,
                            backGround.transform.position.y
                        );
                    }
                }
                else
                {
                    // Timeline中などはカメラに直接追従
                    backGround.transform.position = new Vector2(
                        cameraPosition.x,
                        backGround.transform.position.y
                    );
                }
            }
            yield return null;
        }

        backgroundMoveCoroutine = null;
    }

    #endregion

    #region Editor / Debug

    private void OnDrawGizmos()
    {
        BoxCollider2D box2D = GetComponent<BoxCollider2D>();
        if (box2D == null)
            return;

        Color fillColor = new Color(1f, 0f, 1f, 0.2f); // 半透明のマゼンタ
        Color borderColor = Color.magenta;

        Vector3 centerPos = transform.position + (Vector3)box2D.offset;
        Gizmos.matrix = Matrix4x4.TRS(centerPos, transform.rotation, transform.lossyScale);

        Gizmos.color = fillColor;
        Gizmos.DrawCube(Vector3.zero, (Vector3)box2D.size);

        Gizmos.color = borderColor;
        Gizmos.DrawWireCube(Vector3.zero, (Vector3)box2D.size);

#if UNITY_EDITOR
        // エディタ上でのラベル表示
        string labelText = gameObject.name;
        string[] splitName = labelText.Split('_');
        if (splitName.Length > 1)
            labelText = splitName[splitName.Length - 1];

        GUIStyle style = new GUIStyle();
        style.normal.textColor = Color.white;
        style.alignment = TextAnchor.MiddleCenter;
        style.fontSize = 12;
        style.fontStyle = FontStyle.Bold;

        // Gizmos.matrixの影響を受けないため、ワールド座標で描画
        Handles.Label(centerPos, labelText, style);
#endif
    }

    #endregion
}
