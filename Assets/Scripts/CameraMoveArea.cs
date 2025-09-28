using System;
using System.Collections;
using System.Collections.Generic;
using Cinemachine;
using MyGame.CameraControl;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// プレイヤーが特定のエリアに入るとカメラの境界、Volume Profile、2Dライトの形状を設定します。
/// エリア外に出ると元に戻します。
/// </summary>
[RequireComponent(typeof(CompositeCollider2D))]
public class CameraMoveArea : MonoBehaviour
{
    /// <summary>
    /// 現在プレイヤーがいる、アクティブなCameraMoveAreaのインスタンス。
    /// </summary>
    private static CameraMoveArea activeArea = null;

    /// <summary>
    /// プレイヤーが、いずれかのCameraMoveAreaに入ったときに発行されるイベント。
    /// 引数には、入ったエリアのインスタンス自身が含まれます。
    /// </summary>
    public static event Action<CameraMoveArea> OnPlayerEnteredArea;

    /// <summary>
    /// プレイヤーが、アクティブだったCameraMoveAreaから出たときに発行されるイベント。
    /// 引数には、出ていったエリアのインスタンス自身が含まれます。
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
            // アクティブなエリアのインスタンスとそのコライダーが存在するかをチェック
            if (activeArea != null && activeArea.areaCollider != null)
            {
                // コライダーのboundsプロパティは、ワールド空間での境界を返す
                return activeArea.areaCollider.bounds;
            }
            // アクティブなエリアがなければnullを返す
            return null;
        }
    }

    /// <summary>
    /// 現在プレイヤーがいるアクティブなエリアのBGMをフェードインで再生します。
    /// どのスクリプトからでも呼び出し可能です。
    /// </summary>
    /// <param name="fadeDuration">フェードインにかける時間（秒）</param>
    public static void PlayCurrentAreaBgm(float fadeDuration)
    {
        // BGMManagerまたはアクティブなエリアが存在しない場合は何もしない
        if (BGMManager.instance == null || activeArea == null)
        {
            Debug.LogWarning(
                "BGMManagerのインスタンスまたはアクティブなエリアが見つからないため、BGMを再生できません。"
            );
            return;
        }

        // 1. 現在アクティブなエリアで再生すべきBGMカテゴリを取得
        BGMCategory bgmToPlay = activeArea.GetBgmForCurrentFlags();

        // 2. BGMManagerにフェードイン再生を指示
        BGMManager.instance.FadeIn(bgmToPlay, fadeDuration);
    }

    [Header("追従する背景オブジェクト")]
    [Tooltip("カメラに追従して動かしたい背景のGameObject")]
    [SerializeField]
    private GameObject backGround;

    [Header("ポストプロセス設定")]
    [Tooltip("このエリア内に入ったときに適用するVolume Profile")]
    [SerializeField]
    private VolumeProfile areaVolumeProfile;

    [Header("2Dライト設定")]
    [Tooltip("形状をこのエリアのコライダーに合わせたいFreeform Light 2D")]
    [SerializeField]
    private Light2D areaLight;

    [Header("BGM設定")]
    [Tooltip("どのフラグ条件にも一致しない場合に再生される、デフォルトのBGM")]
    [SerializeField]
    private BGMCategory defaultBgm;

    [Tooltip(
        "特定のフラグが立っている場合に、優先的に再生するBGMのリスト。上にあるものほど優先度が高いです。"
    )]
    [SerializeField]
    private List<ConditionalBgm> conditionalBgms = new List<ConditionalBgm>();

    // シーン全体のVolumeコンポーネントとその元のプロファイル
    private Volume globalVolume;

    // Cinemachine Confiner2Dの境界となるCollider
    private CompositeCollider2D areaCollider;

    // プレイヤーのTransformと、カメラYオフセット
    private Transform playerTransform;
    private float cameraOffsetY;

    // カメラの横幅の半分
    private float cameraHalfWidth;

    //エリア進入時にカメラのY軸追従をリセットする時間（秒）。0で無効。
    private float yDampingResetDuration = 0.2f;

    // 背景の移動を制御するコルーチンへの参照
    private Coroutine backgroundMoveCoroutine = null;
    private Vector2 defaultBackgroundPosition = Vector2.zero;

    // プレイヤーがエリア内にいるかどうかを示すフラグ
    private bool isPlayerInArea = false;

    /// <summary>
    /// オブジェクトの初期化を行います。
    /// </summary>
    private void Awake()
    {
        // 自身のCompositeCollider2Dを取得
        areaCollider = GetComponent<CompositeCollider2D>();

        // シーン内のVolumeコンポーネントを検索してキャッシュ
        globalVolume = FindObjectOfType<Volume>();
        if (globalVolume == null)
        {
            Debug.LogWarning("シーン内にVolumeコンポーネントが見つかりません。", this);
        }

        //Lightの形状を更新
        UpdateLightShapeToCollider();

        // メインカメラのコンポーネントが利用可能かチェック
        if (Camera.main == null)
        {
            Debug.LogError(
                "メインカメラが見つかりません。タグが'MainCamera'であることを確認してください。"
            );
            return;
        }

        // カメラの横幅の半分を計算
        cameraHalfWidth = Camera.main.orthographicSize * Camera.main.aspect;

        // 背景の初期位置を保存
        if (backGround != null)
        {
            defaultBackgroundPosition = backGround.transform.position;
        }
    }

    /// <summary>
    /// エディタで値が変更されたときに呼び出される
    /// </summary>
    private void OnValidate()
    {
        // CompositeCollider2Dがなければ取得を試みる
        if (areaCollider == null)
        {
            areaCollider = GetComponent<CompositeCollider2D>();
        }
        UpdateLightShapeToCollider();
    }

    /// <summary>
    /// areaLightの形状をareaColliderの形状に合わせる
    /// </summary>
    private void UpdateLightShapeToCollider()
    {
        if (areaLight != null)
            areaLight.gameObject.SetActive(true); // Light2Dをアクティブにする

        // LightまたはColliderが設定されていなければ何もしない
        if (areaLight == null || areaCollider == null)
            return;

        // CompositeColliderのパスポイントを取得
        if (areaCollider.pathCount > 0)
        {
            Vector2[] pathPoints = new Vector2[areaCollider.GetPathPointCount(0)];
            areaCollider.GetPath(0, pathPoints);

            // Light2DのShapePathに設定するためにVector3配列に変換
            Vector3[] lightPath = new Vector3[pathPoints.Length];
            for (int i = 0; i < pathPoints.Length; i++)
            {
                // Colliderのローカル座標をワールド座標に変換
                Vector3 worldPoint = transform.TransformPoint(pathPoints[i]);
                // ワールド座標をLight2Dのローカル座標に変換
                lightPath[i] = areaLight.transform.InverseTransformPoint(worldPoint);
            }

            // Light2DのShapeを更新
            areaLight.SetShapePath(lightPath);
        }
    }

    /// <summary>
    /// オブジェクトに衝突したときに呼び出されます。（Trigger設定時）
    /// </summary>
    /// <param name="other">衝突したCollider2D</param>
    private void OnTriggerEnter2D(Collider2D other)
    {
        // 衝突したのがプレイヤーかチェック
        if (other.CompareTag(GameConstants.PlayerTagName))
        {
            // もし他のエリアがアクティブだった場合、そのエリアの終了処理を先に呼び出す
            if (activeArea != null && activeArea != this)
            {
                activeArea.HandlePlayerExit();
            }
            activeArea = this; // 自分をアクティブなエリアとして設定
            // カメラのY軸Dampingを一時的にリセットするようCameraManagerに依頼
            if (yDampingResetDuration > 0 && CameraManager.instance != null)
            {
                CameraManager.instance.TriggerTemporaryDampingReset(yDampingResetDuration);
            }

            PlayBgmBasedOnFlags(); // フラグに基づいてBGMを再生

            if (areaLight != null)
                areaLight.gameObject.SetActive(true); // Light2Dをアクティブにする

            // プレイヤーのTransformとカメラオフセットを取得
            playerTransform = other.transform;
            cameraOffsetY = GameConstants.PLAYER_CAMERA_FOLLOW_OFFSET.y;

            isPlayerInArea = true;
            ApplyAreaSettings(); // エリア設定を適用

            // 背景移動コルーチンが動いていない場合、開始する
            if (backgroundMoveCoroutine == null)
            {
                backgroundMoveCoroutine = StartCoroutine(MoveBackgroundWithCamera());
            }

            OnPlayerEnteredArea?.Invoke(this); // イベントを発行
        }
    }

    // プレイヤーがエリアに入ったときの処理をまとめたメソッド
    private void ApplyAreaSettings()
    {
        // Volume Profileを切り替える
        if (globalVolume != null && areaVolumeProfile != null)
        {
            globalVolume.profile = areaVolumeProfile;
        }

        // CinemachineConfiner2Dの境界を設定
        StartCoroutine(SetBoundingShape());
    }

    // プレイヤーがエリアから出たときの処理をまとめたメソッド
    private void HandlePlayerExit()
    {
        isPlayerInArea = false;

        if (areaLight != null)
            areaLight.gameObject.SetActive(false); // Light2Dを非アクティブにする

        if (backgroundMoveCoroutine != null)
        {
            StopCoroutine(backgroundMoveCoroutine);
            backgroundMoveCoroutine = null;
        }

        if (backGround != null)
        {
            backGround.transform.position = defaultBackgroundPosition;
        }

        // イベントを発行
        OnPlayerExitedArea?.Invoke(this);
    }

    /// <summary>
    /// CinemachineConfiner2Dの境界をこのエリアのColliderに設定するコルーチン。
    /// Confiner2Dが有効になるまで試行します。
    /// </summary>
    private IEnumerator SetBoundingShape()
    {
        CinemachineConfiner2D confiner = Camera.main.GetComponent<CinemachineConfiner2D>();

        // CinemachineConfiner2Dが見つからない場合はログを出して終了
        if (confiner == null)
        {
            Debug.LogError("CinemachineConfiner2Dがメインカメラに見つかりません。");
            yield break;
        }

        // BoundingShape2Dが設定されるまで最大10回試行
        for (int i = 0; i < 10; i++)
        {
            // BoundingShapeにareaColliderを設定
            confiner.m_BoundingShape2D = areaCollider;

            // 設定が成功したか確認
            if (confiner.m_BoundingShape2D != null)
            {
                //Debug.Log("Bounding Shapeが正常に設定されました。");
                yield break; // 成功したらコルーチンを終了
            }

            // 次のフレームで再試行
            yield return null;
        }

        // 試行回数を超えても設定できなかった場合
        Debug.LogWarning("CinemachineConfiner2DのBounding Shape設定に失敗しました。");
    }

    /// <summary>
    /// カメラの位置に基づいて背景を追従移動させるコルーチン。
    /// </summary>
    private IEnumerator MoveBackgroundWithCamera()
    {
        while (isPlayerInArea)
        {
            if (backGround != null)
            {
                // 背景が非アクティブならアクティブにする
                if (!backGround.activeSelf)
                {
                    backGround.SetActive(true);
                }

                // カメラの位置情報を取得し、背景の位置を更新
                Vector3 cameraPosition = Camera.main.transform.position;
                Vector3 playerPosition =
                    (playerTransform != null) ? playerTransform.position : Vector3.zero;

                // カメラの追従が有効な場合
                if (Camera.main.GetComponent<CinemachineBrain>().enabled)
                {
                    // カメラの境界情報を取得
                    string cameraAtEdge = Camera
                        .main.GetComponent<CameraBoundaryChecker>()
                        .CameraAtEdge;

                    if (playerTransform != null)
                    {
                        // プレイヤーのy座標をカメラオフセットで調整
                        Vector3 adjustedPlayerPos = playerPosition;
                        adjustedPlayerPos.y += cameraOffsetY;

                        // カメラが境界に達しているかチェックし、背景の移動を調整
                        if (cameraAtEdge == "left")
                        {
                            adjustedPlayerPos.x = areaCollider.bounds.min.x + cameraHalfWidth;
                        }
                        else if (cameraAtEdge == "right")
                        {
                            adjustedPlayerPos.x = areaCollider.bounds.max.x - cameraHalfWidth;
                        }

                        // 背景のx座標をプレイヤーの位置に設定
                        backGround.transform.position = new Vector2(
                            adjustedPlayerPos.x,
                            backGround.transform.position.y
                        );
                    }
                }
                else // カメラの追従が無効な場合（手動カメラ移動など）
                {
                    // 単純にカメラのx座標に背景を追従させる
                    backGround.transform.position = new Vector2(
                        cameraPosition.x,
                        backGround.transform.position.y
                    );
                }
            }
            yield return null; // 1フレーム待機
        }

        backgroundMoveCoroutine = null;
    }

    private void PlayBgmBasedOnFlags()
    {
        if (BGMManager.instance == null)
            return;

        // 再生すべきBGMを取得
        BGMCategory bgmToPlay = GetBgmForCurrentFlags();

        // BGMを再生（エリア進入時は即時再生）
        BGMManager.instance.Play(bgmToPlay);
    }

    /// <summary>
    /// 現在のフラグ状況に基づいて、再生すべきBGMカテゴリを返します。
    /// </summary>
    /// <returns>再生すべきBGMのカテゴリ</returns>
    public BGMCategory GetBgmForCurrentFlags()
    {
        // 条件付きBGMリストを上から順に（＝優先度が高い順に）評価
        foreach (var condition in conditionalBgms)
        {
            if (condition.AreConditionsMet())
            {
                // 条件に一致したBGMを返す
                return condition.bgmToPlay;
            }
        }

        // どの条件にも一致しなかった場合、デフォルトのBGMを返す
        return defaultBgm;
    }

    /// <summary>
    /// 開発用に、エリアの境界をSceneビューに描画します。
    /// </summary>
    private void OnDrawGizmos()
    {
        // ギズモの描画色と透明度を設定（半透明のマゼンタ）
        Color fillColor = new Color(1f, 0f, 1f, 0.05f);
        Color borderColor = Color.magenta;

        // BoxCollider2Dが存在するかチェック
        BoxCollider2D box2D = GetComponent<BoxCollider2D>();
        if (box2D == null)
            return;

        // Gizmoの描画行列を設定し、オブジェクトの回転やスケールを考慮
        Gizmos.matrix = Matrix4x4.TRS(
            transform.position + (Vector3)box2D.offset,
            transform.rotation,
            transform.lossyScale
        );

        // 塗りつぶしの立方体を描画
        Gizmos.color = fillColor;
        Gizmos.DrawCube(Vector3.zero, (Vector3)box2D.size);

        // 輪郭線を描画
        Gizmos.color = borderColor;
        Gizmos.DrawWireCube(Vector3.zero, (Vector3)box2D.size);
    }
}
