using System.Collections;
using System.Collections.Generic;
using Shapes2D;
using UnityEngine;

/// <summary>
/// 始点と終点の間を往復する2Dプラットフォーム（リフト）。
/// FixedUpdateで物理的に移動し、プレイヤーを乗せて一緒に動きます。
/// 横幅に応じてスプライトを動的に生成・配置します。
/// </summary>
[RequireComponent(typeof(BoxCollider2D))] // リフトにはBoxCollider2Dが必須
public class MovingPlatform : MonoBehaviour
{
    [Header("リフトの移動設定")]
    [Tooltip("移動の始点となるローカル座標")]
    [SerializeField]
    private Vector2 startLocalPosition;

    [Tooltip("移動の終点となるローカル座標")]
    [SerializeField]
    private Vector2 endLocalPosition;

    [Tooltip("リフトの移動速度 (単位: ユニット/秒)")]
    [SerializeField]
    private float speed = 2.0f;

    [Tooltip("終点に到達してから折り返すまでの待機時間（秒）")]
    [SerializeField]
    private float waitTimeAtEnds = 1.0f;

    [Header("リフトの見た目設定")]
    [Tooltip("リフトの中央部のスプライト")]
    [SerializeField]
    private Sprite middleSprite;

    [Tooltip("リフトの端のスプライト (左右両方に使用)")]
    [SerializeField]
    private Sprite endSprite;

    [Tooltip("リフトの横幅 (ユニット単位)。スプライトの幅の倍数で設定することを推奨します。")]
    [SerializeField]
    private int platformWidthUnits = 5;
    private string sortingLayerName = "Ground";
    private int orderInLayer = 0;
    private Vector2 targetWorldPosition; // リフトの現在の目標位置（ワールド座標）
    private bool movingToEnd = true; // 現在の移動方向 (始点→終点: true, 終点→始点: false)
    private float waitTimer = 0.0f; // 終点に到達した後の待機時間を計測するタイマー
    private bool isWaiting = false; // リフトが待機状態かどうかのフラグ
    private Rigidbody2D rbody;

    private void Awake()
    {
        // 初期位置が設定されていない場合はエラーメッセージを表示
        if (startLocalPosition == Vector2.zero || endLocalPosition == Vector2.zero)
        {
            Debug.LogError($"{this.name}の始点または終点が設定されていません");
        }

        // スプライトが設定されていない場合はエラーメッセージを表示
        if (middleSprite == null || endSprite == null)
        {
            Debug.LogError(
                $"{this.name}のリフトスプライトが設定されていません。Middle SpriteとEnd SpriteをInspectorで設定してください。"
            );
        }

        rbody = GetComponent<Rigidbody2D>();

        // リフトのスプライトを動的に生成
        GeneratePlatformSprites();
    }

    /// <summary>
    /// リフトの初期化を行います。
    /// </summary>
    private void Start()
    {
        // 初期位置を始点に設定
        transform.localPosition = startLocalPosition;
        // 最初の目標位置を終点に設定
        targetWorldPosition = GetWorldPosition(endLocalPosition);
    }

    /// <summary>
    /// 物理フレームごとにリフトの移動を更新します。
    /// </summary>
    private void FixedUpdate()
    {
        // 待機状態ならタイマーを更新
        if (isWaiting)
        {
            waitTimer += Time.fixedDeltaTime;
            if (waitTimer >= waitTimeAtEnds)
            {
                // 待機時間が終了したら移動を再開
                isWaiting = false;
                waitTimer = 0.0f;
                // 移動方向を反転し、新しい目標位置を設定
                movingToEnd = !movingToEnd;
                targetWorldPosition = GetWorldPosition(
                    movingToEnd ? endLocalPosition : startLocalPosition
                );
            }
            return; // 待機中は移動しない
        }

        // 現在位置から目標位置までの距離を計算
        float distance = Vector2.Distance(transform.position, targetWorldPosition);

        // 目標位置に十分近づいたら、位置を補正して待機状態へ移行
        if (distance <= 0.01f)
        {
            transform.position = targetWorldPosition; // 位置を正確に補正
            isWaiting = true; // 待機状態に移行
            return; // 待機状態に移行したため、このフレームの移動は終了
        }

        // 移動ステップを計算
        Vector2 direction = (targetWorldPosition - (Vector2)transform.position).normalized;
        Vector2 moveStep = direction * speed * Time.fixedDeltaTime;

        // Rigidbody2Dを使って物理的に移動させる
        rbody.MovePosition((Vector2)transform.position + moveStep);
    }

    /// <summary>
    /// リフトの横幅に応じて、中央と端のスプライトを動的に生成・配置します。
    /// </summary>
    private void GeneratePlatformSprites()
    {
        // 既存のスプライト子オブジェクトをすべて削除して再生成
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(transform.GetChild(i).gameObject);
        }

        // スプライトの幅をユニット単位で計算
        float spriteWidthUnit = middleSprite.rect.width / GameConstants.PIXELS_PER_UNIT;

        // 生成するスプライトの総数を計算
        int numSprites = platformWidthUnits;

        if (numSprites < 2)
        {
            Debug.LogWarning("リフトの横幅は2ユニット以上にする必要があります。");
            return;
        }

        // リフトの左端の開始位置
        float startX = -(numSprites / 2f) * spriteWidthUnit + spriteWidthUnit / 2f;

        // リフトのコライダーを調整
        BoxCollider2D boxCollider = GetComponent<BoxCollider2D>();
        boxCollider.size = new Vector2(platformWidthUnits, spriteWidthUnit);
        boxCollider.offset = Vector2.zero; // 中央に配置

        // 左右の端のスプライトを配置
        CreateSpriteObject("LeftEndSprite", endSprite, new Vector2(startX, 0), true);
        CreateSpriteObject(
            "RightEndSprite",
            endSprite,
            new Vector2(startX + (numSprites - 1) * spriteWidthUnit, 0),
            false
        );

        // 中央のスプライトを配置
        for (int i = 1; i < numSprites - 1; i++) // 両端を除く
        {
            float xPos = startX + i * spriteWidthUnit;
            CreateSpriteObject($"MiddleSprite_{i}", middleSprite, new Vector2(xPos, 0), false);
        }
    }

    /// <summary>
    /// 指定されたスプライトを使って子オブジェクトを生成し、描画順を設定します。
    /// </summary>
    /// <param name="name">生成するオブジェクトの名前</param>
    /// <param name="sprite">設定するスプライト</param>
    /// <param name="localPosition">親オブジェクトからのローカル座標</param>
    /// <param name="flipX">スプライトを水平反転するかどうか</param>
    private void CreateSpriteObject(string name, Sprite sprite, Vector2 localPosition, bool flipX)
    {
        GameObject newSpriteObject = new GameObject(name);
        newSpriteObject.transform.SetParent(this.transform);
        newSpriteObject.transform.localPosition = localPosition;

        SpriteRenderer spriteRenderer = newSpriteObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = sprite;
        spriteRenderer.flipX = flipX;

        // Sorting LayerとOrder in Layerを設定
        spriteRenderer.sortingLayerName = sortingLayerName;
        spriteRenderer.sortingOrder = orderInLayer;
    }

    /// <summary>
    /// ローカル座標をワールド座標に変換します。
    /// </summary>
    /// <param name="localPosition">変換するローカル座標</param>
    /// <returns>対応するワールド座標</returns>
    private Vector2 GetWorldPosition(Vector2 localPosition)
    {
        return transform.parent != null
            ? (Vector2)transform.parent.TransformPoint(localPosition)
            : localPosition;
    }

    /// <summary>
    /// オブジェクトがリフトに乗ったときに呼び出されます。
    /// </summary>
    /// <param name="other">衝突したCollider2D</param>
    private void OnTriggerEnter2D(Collider2D other)
    {
        // プレイヤーに"Player"タグが設定されていることを前提とします。
        if (other.CompareTag(GameConstants.PlayerTagName))
        {
            // プレイヤーをリフトの子オブジェクトにする
            other.transform.SetParent(this.transform);
        }
    }

    /// <summary>
    /// オブジェクトがリフトから降りたときに呼び出されます。
    /// </summary>
    /// <param name="other">衝突が終了したCollider2D</param>
    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag(GameConstants.PlayerTagName))
        {
            // プレイヤーの親オブジェクトを解除
            other.transform.SetParent(null);
        }
    }

    /// <summary>
    /// リフトの移動範囲と横幅の軌跡を常にシーンビューに表示します。
    /// </summary>
    private void OnDrawGizmos()
    {
        // Gizmos描画に必要な情報が揃っているか確認
        if (middleSprite == null)
        {
            // middleSpriteが設定されていない場合は、リフトの幅が不明なため描画しない
            return;
        }

        // リフトの始点と終点のワールド座標を取得
        Vector3 startWorldPosition = GetWorldPosition(startLocalPosition);
        Vector3 endWorldPosition = GetWorldPosition(endLocalPosition);

        // リフトの横幅をユニット単位で計算
        // GeneratePlatformSprites()と同じロジックを使用
        float platformWidth = platformWidthUnits;

        // リフトの上下の端の位置を計算
        // スプライトの高さの半分をオフセットとして使用
        float spriteHeightUnit = middleSprite.rect.height / GameConstants.PIXELS_PER_UNIT;

        // リフトの横幅を考慮した、移動範囲の軌跡（長方形）を描画
        Vector3 size = new Vector3(platformWidth, spriteHeightUnit, 0.1f);

        // リフトの軌跡を始点から終点まで細かく描画
        // 始点と終点にリフトを置いた時の境界を描画する

        Gizmos.color = Color.cyan; // 始点終点の色をシアンに設定
        // 始点のリフトの描画
        Vector3 startBoxCenter = startWorldPosition;
        Gizmos.DrawWireCube(startBoxCenter, size);

        // 終点のリフトの描画
        Vector3 endBoxCenter = endWorldPosition;
        Gizmos.DrawWireCube(endBoxCenter, size);

        // リフトが移動する経路全体をカバーする長方形の領域を描画
        Vector3 trajectoryCenter = (startWorldPosition + endWorldPosition) / 2f;
        Vector3 trajectoryDirection = (endWorldPosition - startWorldPosition).normalized;
        float trajectoryDistance = Vector3.Distance(startWorldPosition, endWorldPosition);

        // 経路の長さ + リフトの横幅で全体のサイズを計算
        Vector3 trajectorySize = new Vector3(
            trajectoryDirection.x == 0 ? platformWidth : trajectoryDistance + platformWidth,
            trajectoryDirection.y == 0 ? platformWidth : trajectoryDistance + platformWidth,
            size.z
        );

        // XとYの移動距離に基づいてサイズを調整
        trajectorySize.x = Mathf.Abs(startWorldPosition.x - endWorldPosition.x) + platformWidth;
        trajectorySize.y = Mathf.Abs(startWorldPosition.y - endWorldPosition.y) + spriteHeightUnit;

        // 軌跡全体を半透明の塗りつぶしで表示
        Gizmos.color = new Color(0.5f, 0f, 1f, 0.2f); // 非常に薄い紫色の塗りつぶし
        Gizmos.DrawCube(trajectoryCenter, trajectorySize);
    }
}
