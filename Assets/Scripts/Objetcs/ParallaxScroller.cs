using UnityEngine;
using System.Linq;

/// <summary>
/// 複数の背景オブジェクトを連続的に横スクロールさせるコンポーネント。
/// 親オブジェクトの移動を考慮するかどうかをスクロール速度に応じて自動で判断します。
/// </summary>
public class ParallaxScroller : MonoBehaviour
{
    [Header("背景設定")]
    [Tooltip("横に並べてスクロールさせる背景のGameObjectのリスト")]
    [SerializeField]
    private GameObject[] backgrounds;

    [Header("スクロール設定")]
    [Tooltip("背景が左にスクロールする基本速度（マイナスにすると右に動きます）")]
    [SerializeField]
    private float scrollSpeed = 2f;
    
    // この値よりscrollSpeedが速い場合、親の動きを補正して見た目の速度を一定に保ちます
    private const float COMPENSATION_THRESHOLD = 10f; 

    // --- 外部からアクセス可能なプロパティ ---
    public float ScrollSpeed
    {
        get { return scrollSpeed; }
        set { scrollSpeed = value; }
    }

    // --- 内部で利用する変数 ---
    private float backgroundWidth;
    private Vector3 lastParentPosition;
    private Renderer[] backgroundRenderers;

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
            Debug.LogError("背景オブジェクトにSpriteRendererやスプライトが設定されていません。", this);
            return;
        }
        backgroundWidth = firstBgRenderer.sprite.bounds.size.x;

        // --- 背景を横一列に整列 ---
        for (int i = 0; i < backgrounds.Length; i++)
        {
            backgrounds[i].transform.localPosition = new Vector3(backgroundWidth * i, 0, 0);
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
        // 【修正】画面外でもループ処理を継続させるため、可視判定による中断を無効化
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
}