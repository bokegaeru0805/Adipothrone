using System;
using System.Collections;
using System.Collections.Generic;
using Cinemachine;
using MyGame.CameraControl;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine.SceneManagement;
#endif

/// <summary>
/// プレイヤーの進入に応じて、カメラ境界、背景、Volume、BGM、2Dライトを切り替えるエリアです。
/// Timelineからカメラだけを移動する場合の強制アクティブ化にも対応します。
/// </summary>
[RequireComponent(typeof(CompositeCollider2D))]
public class CameraMoveArea : MonoBehaviour
{
    #region 列挙型・定数

    /// <summary>
    /// エリア上辺におけるFreeform Lightの配置基準点です。
    /// </summary>
    private enum AreaLightPositionOrigin
    {
        [InspectorName("左上")]
        TopLeft = 0,
        [InspectorName("中央上")]
        TopCenter = 1,
        [InspectorName("右上")]
        TopRight = 2,
    }

    private const float LightShapePointToleranceSqr = 0.00000001f;

    #endregion

    #region 静的状態・イベント

    /// <summary>
    /// 現在アクティブなエリアです。
    /// </summary>
    private static CameraMoveArea activeArea = null;

    /// <summary>
    /// プレイヤーがエリアへ入ったときに通知します。
    /// </summary>
    public static event Action<CameraMoveArea> OnPlayerEnteredArea;

    /// <summary>
    /// アクティブだったエリアが終了したときに通知します。
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
    /// イベント中などにエリアBGMで上書きされることを防ぎます。
    /// </summary>
    private static bool isAreaBgmLocked = false;

    /// <summary>
    /// ドメインリロードを無効にしたEditor環境でも静的状態が残らないよう初期化します。
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        activeArea = null;
        isAreaBgmLocked = false;
    }

    /// <summary>
    /// シーン遷移時にアクティブエリアとBGMロックをリセットするコールバックを登録します。
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneChangeCallback()
    {
        // ドメインリロード無効時の多重登録を防ぐ。
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

    #region Inspector設定

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

    [Tooltip("Freeform Light 2Dの中心位置を配置する際の基準点")]
    [SerializeField, EnableIf(nameof(IsAreaLightFreeform))]
    private AreaLightPositionOrigin areaLightPositionOrigin = AreaLightPositionOrigin.TopLeft;

    [Tooltip("選択した基準点からFreeform Light 2Dの中心位置に加えるオフセット")]
    [SerializeField]
    [EnableIf(nameof(IsAreaLightFreeform))]
    [FormerlySerializedAs("areaLightOffsetFromLeft")]
    [FormerlySerializedAs("areaLightOffsetFromTopLeft")]
    private Vector2 areaLightPositionOffset = Vector2.zero;

    private bool IsAreaLightFreeform =>
        areaLight != null && areaLight.lightType == Light2D.LightType.Freeform;

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
    [Tooltip("このエリア内に入った時、カメラをエリア（コライダー）の中央に完全に固定するかどうか")]
    [SerializeField]
    private bool lockCameraToCenter = false;

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

    #region 内部状態・参照

    private CompositeCollider2D areaCollider;
    private Transform playerTransform;

    private float cameraOffsetY;
    private float cameraHalfWidth;
    private float yDampingResetDuration = 0.2f;

    private Coroutine backgroundMoveCoroutine = null;
    private Vector2 defaultBackgroundPosition = Vector2.zero;

    private bool isPlayerInArea = false;

#if UNITY_EDITOR
    private bool isDebugScene = false;
#endif

    #endregion

    #region Unityイベント

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
        if (!other.CompareTag(GameConstants.PLAYER_TAG_NAME) || activeArea == this)
            return;

        if (activeArea != null)
        {
            activeArea.HandlePlayerExit();
        }

        HandlePlayerEnter(other);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag(GameConstants.PLAYER_TAG_NAME))
            return;

        isPlayerInArea = false;

        // エリア間の余白で背景・カメラ境界・Lightが途切れないよう、ここでは終了処理を行わない。
        // HandlePlayerExitは、次のエリアへ進入した時点で旧activeAreaに対して呼び出される。
    }

    #endregion

    #region 初期化・エリア進入退出

    /// <summary>
    /// 必須コンポーネントを取得し、Light形状とカメラ幅を初期化します。
    /// </summary>
    private void InitializeComponents()
    {
        areaCollider = GetComponent<CompositeCollider2D>();

        UpdateLightShapeToCollider();

        if (Camera.main == null)
        {
            Debug.LogError("メインカメラが見つかりません。", this);
            return;
        }

        cameraHalfWidth = Camera.main.orthographicSize * Camera.main.aspect;
    }

    /// <summary>
    /// プレイヤー進入時に、このエリア固有の設定を有効化します。
    /// </summary>
    private void HandlePlayerEnter(Collider2D playerCollider)
    {
        activeArea = this;
        isPlayerInArea = true;

        playerTransform = playerCollider.transform;
        cameraOffsetY = GameConstants.PLAYER_CAMERA_FOLLOW_OFFSET.y;

        // Dampingによる追従遅れでカメラが新しい境界へめり込むことを防ぐ。
        if (yDampingResetDuration > 0 && CameraManager.instance != null)
        {
            CameraManager.instance.TriggerTemporaryDampingReset(yDampingResetDuration);
        }

        if (!isAreaBgmLocked)
        {
            PlayBgmBasedOnFlags();
        }

        if (areaLight != null)
            areaLight.gameObject.SetActive(true);

        ApplyAreaSettings();

        if (lockCameraToCenter && CameraManager.instance != null)
        {
            CameraManager.instance.SetAreaCameraLock(true, areaCollider.bounds.center);
        }

        if (backgroundMoveCoroutine == null)
        {
            backgroundMoveCoroutine = StartCoroutine(MoveBackgroundWithCamera());
        }

        OnPlayerEnteredArea?.Invoke(this);
    }

    /// <summary>
    /// Volume、カメラ個別設定、Cinemachine Confinerを適用します。
    /// </summary>
    private void ApplyAreaSettings()
    {
        if (GlobalVolumeManager.instance != null && areaVolumeProfile != null)
        {
            GlobalVolumeManager.instance.ChangeProfileImmediate(areaVolumeProfile);
        }

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

        StartCoroutine(SetBoundingShape());
    }

    /// <summary>
    /// このエリアで有効化した設定を終了し、退出イベントを通知します。
    /// </summary>
    private void HandlePlayerExit()
    {
        isPlayerInArea = false;

        if (areaLight != null)
            areaLight.gameObject.SetActive(false);

        if (backgroundMoveCoroutine != null)
        {
            StopCoroutine(backgroundMoveCoroutine);
            backgroundMoveCoroutine = null;
        }

        if (backGround != null)
        {
            backGround.transform.position = defaultBackgroundPosition;
            backGround.SetActive(false);
        }

        if (overrideCameraSettings && CameraManager.instance != null)
        {
            CameraManager.instance.ResetCameraSettings(settingsTransitionDuration);
        }

        if (lockCameraToCenter && CameraManager.instance != null)
        {
            CameraManager.instance.SetAreaCameraLock(false, Vector2.zero);
        }

        OnPlayerExitedArea?.Invoke(this);
    }

    #endregion

    #region 外部公開制御

    /// <summary>
    /// シーン内の全てのCameraMoveAreaを走査し、プレイヤーが現在いるエリアを強制的にアクティブにします。
    /// セーブロード時やファストトラベル後の初期化に使用してください。
    /// </summary>
    public static void RefreshActiveArea()
    {
        GameObject player = GameObject.FindGameObjectWithTag(GameConstants.PLAYER_TAG_NAME);
        if (player == null)
            return;

        Collider2D playerCollider = player.GetComponent<Collider2D>();
        if (playerCollider == null)
            return;

        Vector2 playerPos = player.transform.position;

        // 全エリアの探索を伴うため、ロードやワープ直後など必要なタイミングに限定して呼び出す。
        CameraMoveArea[] allAreas = FindObjectsOfType<CameraMoveArea>();

        foreach (var area in allAreas)
        {
            if (area.areaCollider == null)
                area.areaCollider = area.GetComponent<CompositeCollider2D>();

            if (area.areaCollider == null)
                continue;

            if (area.areaCollider.OverlapPoint(playerPos))
            {
                area.HandlePlayerEnter(playerCollider);
                return;
            }

            // ワープなどで以前のエリア外へ移動した場合は、残っている状態を終了する。
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

        if (!isLocked && activeArea != null)
        {
            PlayCurrentAreaBgm(fadeDuration);
        }
    }

    #endregion

    #region BGM制御

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
    /// <returns>条件に一致するBGM。該当しない場合はデフォルトBGM。</returns>
    public BGMCategory GetBgmForCurrentFlags()
    {
        // Inspectorでは進行度が高い条件を下へ置くため、末尾から評価する。
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

    #region Timeline連携

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

        // プレイヤー進入ではないため、BGM切替や進入イベントは発行しない。
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

    #region 2Dライト制御

    /// <summary>
    /// Freeform Lightの中心位置と形状をエリアColliderへ同期します。
    /// </summary>
    public void UpdateLightShapeToCollider()
    {
        TryUpdateLightShapeToCollider();
    }

    /// <summary>
    /// Freeform Lightの中心位置と形状をエリアColliderへ同期します。
    /// </summary>
    /// <returns>位置または形状を変更した場合はtrue。</returns>
    public bool TryUpdateLightShapeToCollider()
    {
        if (areaLight == null || areaCollider == null)
            return false;

        if (areaLight.lightType != Light2D.LightType.Freeform)
            return false;

        bool isChanged = false;
        Bounds areaBounds = areaCollider.bounds;
        Vector3 lightPosition = GetAreaLightPosition(areaBounds);

        if (areaLight.transform.position != lightPosition)
        {
            areaLight.transform.position = lightPosition;
            isChanged = true;
        }

        if (areaCollider.pathCount > 0)
        {
            // Colliderのローカル頂点を一度ワールド座標へ変換し、移動後のLightローカル座標へ変換する。
            // これによりLightのTransformを基準点へ移しても、照明範囲はCollider全体と一致する。
            Vector2[] pathPoints = new Vector2[areaCollider.GetPathPointCount(0)];
            areaCollider.GetPath(0, pathPoints);

            Vector3[] lightPath = new Vector3[pathPoints.Length];
            for (int i = 0; i < pathPoints.Length; i++)
            {
                Vector3 worldPoint = transform.TransformPoint(pathPoints[i]);
                lightPath[i] = areaLight.transform.InverseTransformPoint(worldPoint);
            }

            if (IsLightShapeChanged(areaLight.shapePath, lightPath))
            {
                areaLight.SetShapePath(lightPath);
                isChanged = true;
            }
        }

        return isChanged;
    }

    /// <summary>
    /// 選択された上辺の基準点とオフセットから、Light中心のワールド座標を求めます。
    /// Z座標は既存値を維持します。
    /// </summary>
    private Vector3 GetAreaLightPosition(Bounds areaBounds)
    {
        Vector3 lightPosition = areaLight.transform.position;

        switch (areaLightPositionOrigin)
        {
            case AreaLightPositionOrigin.TopCenter:
                lightPosition.x = areaBounds.center.x;
                break;
            case AreaLightPositionOrigin.TopRight:
                lightPosition.x = areaBounds.max.x;
                break;
            default:
                lightPosition.x = areaBounds.min.x;
                break;
        }

        lightPosition.x += areaLightPositionOffset.x;
        lightPosition.y = areaBounds.max.y + areaLightPositionOffset.y;
        return lightPosition;
    }

    /// <summary>
    /// 頂点数と各頂点の差から、Light形状の更新が必要か判定します。
    /// </summary>
    private static bool IsLightShapeChanged(Vector3[] currentPath, Vector3[] newPath)
    {
        if (currentPath == null || currentPath.Length != newPath.Length)
            return true;

        for (int i = 0; i < newPath.Length; i++)
        {
            if ((currentPath[i] - newPath[i]).sqrMagnitude > LightShapePointToleranceSqr)
                return true;
        }

        return false;
    }

    #endregion

    #region カメラ境界制御

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

        // 初期化順によって代入が反映されない場合に備え、フレームをまたいで再試行する。
        for (int i = 0; i < 10; i++)
        {
            confiner.m_BoundingShape2D = areaCollider;

            if (confiner.m_BoundingShape2D != null)
                yield break;

            yield return null;
        }

        Debug.LogWarning("CinemachineConfiner2DのBounding Shape設定に失敗しました。");
    }

    #endregion

    #region 背景追従

    /// <summary>
    /// 通常時はプレイヤー、Timeline制御時はカメラに背景を追従させます。
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

                if (isBrainEnabled && !isTimelineMode)
                {
                    string cameraAtEdge = Camera
                        .main.GetComponent<CameraBoundaryChecker>()
                        .CameraAtEdge;

                    if (playerTransform != null)
                    {
                        Vector3 adjustedPlayerPos = playerPosition;
                        adjustedPlayerPos.y += cameraOffsetY;

                        // カメラが境界へ達した後も背景だけがプレイヤーへ追従し続けないよう補正する。
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

    #region Editor表示

    private void OnDrawGizmos()
    {
#if UNITY_EDITOR
        // Project独自設定からCameraMoveAreaを含むカスタムGizmoを一括で非表示にできる。
        if (!UnityEditor.EditorPrefs.GetBool("MyGame_ShowCustomGizmos", true))
        {
            return;
        }
#endif

        BoxCollider2D box2D = GetComponent<BoxCollider2D>();
        if (box2D == null)
            return;

        Color fillColor = new Color(1f, 0f, 1f, 0.05f);
        Color borderColor = Color.magenta;

        // Colliderのoffset、回転、ScaleをGizmoへ反映する。
        Gizmos.matrix = transform.localToWorldMatrix;

        Gizmos.color = fillColor;
        Gizmos.DrawCube(box2D.offset, box2D.size);

        Gizmos.color = borderColor;
        Gizmos.DrawWireCube(box2D.offset, box2D.size);

#if UNITY_EDITOR
        string labelText = gameObject.name;
        string[] splitName = labelText.Split('_');
        if (splitName.Length > 1)
            labelText = splitName[splitName.Length - 1];

        GUIStyle style = new GUIStyle();
        style.normal.textColor = Color.white;
        style.alignment = TextAnchor.MiddleCenter;
        style.fontSize = 12;
        style.fontStyle = FontStyle.Bold;

        Vector3 worldCenterPos = transform.TransformPoint((Vector3)box2D.offset);
        UnityEditor.Handles.Label(worldCenterPos, labelText, style);
#endif
    }

    #endregion
}
