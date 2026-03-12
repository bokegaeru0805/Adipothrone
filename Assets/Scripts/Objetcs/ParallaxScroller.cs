using NaughtyAttributes;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 複数の背景オブジェクトを連続的に横スクロールさせるコンポーネント。
/// 親オブジェクトの移動を考慮するかどうかをスクロール速度に応じて自動で判断します。
/// </summary>
public class ParallaxScroller : MonoBehaviour
{
#pragma warning disable 0414 // 使われていない変数の警告（CS0414）を一時的に無効化
    [InfoBox(
        "【背景設定のルール】\n"
            + "背景が途切れないようにするため、以下の条件を満たしてください。\n\n"
            + "条件： (画像の枚数 - 1) × 画像の幅 ＞ カメラの表示横幅\n\n"
            + "※全ての画像は同じサイズである必要があります。"
    )]
    [ReadOnly]
    [SerializeField]
    private string _instruction = "設定不要";
#pragma warning restore 0414 // 警告の無効化を解除（これ以降のコードでは通常通り警告を出す）

    [Header("背景設定")]
    [Tooltip("横に並べてスクロールさせる背景のGameObjectのリスト")]
    [SerializeField]
    private GameObject[] backgrounds;

    [Header("スクロール設定")]
    [Tooltip("背景が左にスクロールする基本速度（マイナスにすると右に動きます）")]
    [SerializeField]
    private float scrollSpeed = 2f;

    [BoxGroup("初期配置設定")]
    [Tooltip("有効にすると、(0,0)ではなく指定したX座標を基準に背景を配置します")]
    [SerializeField]
    private bool setCustomStartPosition = false;

    [BoxGroup("初期配置設定")]
    [Tooltip(
        "配置の基準となるX座標（ローカル）。\n・マイナス値: ここを「先頭（左端）」として右へ並べます\n・プラス値: ここを「末尾（右端）」として左へ並べます"
    )]
    [ShowIf(nameof(setCustomStartPosition))]
    [SerializeField]
    private float startPositionX = 0f;

    // この値よりscrollSpeedが速い場合、親の動きを補正して見た目の速度を一定に保ちます
    private const float COMPENSATION_THRESHOLD = 10f;

    // --- 内部で利用する変数 ---
    private float backgroundWidth;
    private Vector3 lastParentPosition;
    private Renderer[] backgroundRenderers;

    /// 外部（Invoke Methodなど）からスクロール速度を動的に変更します。
    /// </summary>
    /// <param name="newSpeed">新しいスクロール速度（マイナス値にすると右に動きます）</param>
    public void SetScrollSpeed(float newSpeed)
    {
        scrollSpeed = newSpeed;
    }

    void Start()
    {
        // --- 安全確認 ---
        if (backgrounds == null || backgrounds.Length == 0)
        {
            Debug.LogError("背景オブジェクトが設定されていません。", this);
            return;
        }

        backgroundRenderers = new Renderer[backgrounds.Length];

        // --- 背景の横幅を計算 ---
        SpriteRenderer firstBgRenderer = backgrounds[0].GetComponent<SpriteRenderer>();
        if (firstBgRenderer == null || firstBgRenderer.sprite == null)
        {
            Debug.LogError(
                "背景オブジェクトにSpriteRendererやスプライトが設定されていません。",
                this
            );
            return;
        }
        backgroundWidth = firstBgRenderer.sprite.bounds.size.x;

        // 背景を横一列に整列
        // setCustomStartPositionの状態と、startPositionXの正負によって開始位置を決定する

        float initialX = 0f;

        if (setCustomStartPosition)
        {
            if (startPositionX < 0)
            {
                // マイナスなら「左側（一枚目の左端）」を基準にする
                // 1枚目を startPositionX に置き、そこから右へ並べる
                initialX = startPositionX;
            }
            else
            {
                // プラス（または0）なら「右側（最後の背景の右端）」を基準にする
                // 最後の背景の右端が startPositionX になるように逆算する
                // 最後の背景の左端 = startPositionX - backgroundWidth
                // 先頭の背景の左端 = startPositionX - (合計幅)
                initialX = startPositionX - (backgrounds.Length * backgroundWidth);
            }
        }
        else
        {
            // 無効なら従来通り (0,0) スタート
            initialX = 0f;
        }

        for (int i = 0; i < backgrounds.Length; i++)
        {
            // 計算した初期位置(initialX)から順番に並べる
            backgrounds[i].transform.localPosition = new Vector3(
                initialX + (backgroundWidth * i),
                0,
                0
            );
            backgroundRenderers[i] = backgrounds[i].GetComponent<Renderer>();
        }
        lastParentPosition = transform.position;
    }

    private void OnEnable()
    {
        lastParentPosition = transform.position;
    }

    /// <summary>
    /// 全てのUpdate処理が終わった後に呼び出される更新処理
    /// </summary>
    void LateUpdate()
    {
        // 画面外でもループ処理を継続させるため、可視判定による中断を無効化
        // Timeline演出などでカメラを動かしている際、画面外にある背景も裏で動かし続けないと、
        // 「左端から右端へのワープ判定」が行われず、背景が途切れてしまうためです。
        /*
        bool isAnyVisible = backgroundRenderers.Any(r => r != null && r.isVisible);
        if (!isAnyVisible)
        {
            lastParentPosition = transform.position;
            return;
        }
        */

        // --- 1. 親の移動補正を行うか自動で判断 ---
        bool compensate = Mathf.Abs(scrollSpeed) >= COMPENSATION_THRESHOLD; // マイナス速度も考慮して絶対値で判定

        // --- 2. このフレームでの背景の最終的な移動量を計算 ---
        float totalDeltaX;
        // Time.unscaledDeltaTime を使うと、会話中（Time.timeScale=0）でも背景が動きます
        // 止めたい場合は Time.deltaTime に戻してください
        float deltaTime = Time.deltaTime;

        if (compensate)
        {
            Vector3 parentDelta = transform.position - lastParentPosition;
            float baseScrollDeltaX = -scrollSpeed * deltaTime;
            float compensationDeltaX = -parentDelta.x;
            totalDeltaX = baseScrollDeltaX + compensationDeltaX;
        }
        else
        {
            totalDeltaX = -scrollSpeed * deltaTime;
        }

        // --- 3. 全ての背景を移動させる ---
        foreach (var bg in backgrounds)
        {
            bg.transform.localPosition += new Vector3(totalDeltaX, 0, 0);
        }

        // --- 4. 背景のループ処理 ---

        // 移動方向によってループ処理を分岐させます

        // A. 左向き移動（通常）の場合
        if (totalDeltaX < 0)
        {
            GameObject firstBackground = backgrounds[0];
            // 先頭（左端）の背景が、完全に画面左外（-width）に出たら
            if (firstBackground.transform.localPosition.x <= -backgroundWidth)
            {
                // 右端にワープ
                Vector3 newPos = firstBackground.transform.localPosition;
                newPos.x += backgrounds.Length * backgroundWidth;
                firstBackground.transform.localPosition = newPos;

                // 配列の並びを更新（左回転）
                UpdateBackgroundsOrder();
            }
        }
        // B. 右向き移動（逆再生）の場合
        else if (totalDeltaX > 0)
        {
            GameObject lastBackground = backgrounds[backgrounds.Length - 1]; // 配列の末尾（右端）

            // 右向きに動いているので、右端の画像が遠くに行き過ぎていないかチェックしたいところですが、
            // 「左側に隙間ができたら埋める」と考えたほうが確実です。
            // 先頭（左端）の背景が「0」より右に行ってしまったら、左側に隙間ができているということ。
            GameObject firstBackground = backgrounds[0];

            if (firstBackground.transform.localPosition.x > 0)
            {
                // 末尾（右端）にある背景を、先頭の左隣にワープさせる
                Vector3 newPos = firstBackground.transform.localPosition;
                newPos.x -= backgroundWidth; // 先頭のさらに1つ左隣へ
                lastBackground.transform.localPosition = newPos;

                // 配列の並びを更新（右回転）
                UpdateBackgroundsOrderRight();
            }
        }

        // --- 5. 最後に現フレームの親の位置を記録 ---
        lastParentPosition = transform.position;
    }

    /// <summary>
    /// 背景配列の順序を更新し、一番左にあった要素を一番右に移動させます。（左スクロール用）
    /// </summary>
    private void UpdateBackgroundsOrder()
    {
        GameObject first = backgrounds[0];
        System.Array.Copy(backgrounds, 1, backgrounds, 0, backgrounds.Length - 1);
        backgrounds[backgrounds.Length - 1] = first;
    }

    /// <summary>
    /// 背景配列の順序を更新し、一番右にあった要素を一番左に移動させます。（右スクロール用）
    /// </summary>
    private void UpdateBackgroundsOrderRight()
    {
        // 最後の要素（今、左端に移動したもの）を一時的に記憶
        GameObject last = backgrounds[backgrounds.Length - 1];

        // 配列の要素を1つずつ後方にずらす
        System.Array.Copy(backgrounds, 0, backgrounds, 1, backgrounds.Length - 1);

        // 記憶しておいた要素を配列の先頭に設定
        backgrounds[0] = last;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!setCustomStartPosition || backgrounds == null || backgrounds.Length == 0)
            return;

        // 幅が未取得の場合（Editorモード）、SpriteRendererから取得を試みる
        float width = backgroundWidth;
        float height = 5f; // 仮の高さ

        if (width <= 0 && backgrounds[0] != null)
        {
            var sr = backgrounds[0].GetComponent<SpriteRenderer>();
            if (sr != null && sr.sprite != null)
            {
                width = sr.sprite.bounds.size.x;
                height = sr.sprite.bounds.size.y;
            }
            else
            {
                // スプライトがない場合は仮の値
                width = 10f;
            }
        }

        // 親のTransformの影響を受けるためMatrixを設定
        Gizmos.matrix = transform.localToWorldMatrix;

        // 基準線の描画
        Vector3 lineStart = new Vector3(startPositionX, -height, 0);
        Vector3 lineEnd = new Vector3(startPositionX, height, 0);

        // マイナスなら青（左基準）、プラスなら赤（右基準）
        Color baseColor = (startPositionX < 0) ? Color.cyan : Color.red;
        Gizmos.color = baseColor;
        Gizmos.DrawLine(lineStart, lineEnd);

        // 基準点の球
        Gizmos.DrawSphere(new Vector3(startPositionX, 0, 0), 0.2f);

        // 配置範囲の可視化
        float totalWidth = width * backgrounds.Length;
        Vector3 rangeCenter;

        if (startPositionX < 0)
        {
            // 左基準：右に向かって配置される
            rangeCenter = new Vector3(startPositionX + (totalWidth / 2), 0, 0);
            Handles.Label(
                transform.TransformPoint(new Vector3(startPositionX, height + 0.5f, 0)),
                "Left Anchor"
            );
        }
        else
        {
            // 右基準：左に向かって配置される
            rangeCenter = new Vector3(startPositionX - (totalWidth / 2), 0, 0);
            Handles.Label(
                transform.TransformPoint(new Vector3(startPositionX, height + 0.5f, 0)),
                "Right Anchor"
            );
        }

        Gizmos.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0.2f);
        Gizmos.DrawCube(rangeCenter, new Vector3(totalWidth, height * 0.8f, 0));
        Gizmos.color = baseColor;
        Gizmos.DrawWireCube(rangeCenter, new Vector3(totalWidth, height * 0.8f, 0));
    }
#endif
}
