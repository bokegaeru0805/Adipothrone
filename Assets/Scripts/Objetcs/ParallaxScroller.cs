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
    [Tooltip("背景が左にスクロールする基本速度")]
    [SerializeField]
    private float scrollSpeed = 2f;
    private float compensationThreshold = 10f; //この値よりscrollSpeedが速い場合、親の動きを補正して見た目の速度を一定に保ちます

    // --- 内部で利用する変数 ---

    /// <summary>
    /// 背景パネル1枚の横幅（ワールド単位）。ループ処理の基準になります。
    /// </summary>
    private float backgroundWidth;

    /// <summary>
    /// 親オブジェクトの前フレームでのワールド座標。移動量を計算するために使います。
    /// </summary>
    private Vector3 lastParentPosition;

    /// <summary>
    /// 子オブジェクトのRendererコンポーネントのキャッシュ
    /// </summary>
    private Renderer[] backgroundRenderers;

    /// <summary>
    /// ゲーム開始時に一度だけ呼ばれる初期化処理
    /// </summary>
    void Start()
    {
        // --- 安全確認 ---
        if (backgrounds == null || backgrounds.Length == 0)
        {
            Debug.LogError("背景オブジェクトが設定されていません。", this);
            return;
        }

        // Rendererキャッシュ配列を初期化
        backgroundRenderers = new Renderer[backgrounds.Length];

        // --- 背景の横幅を計算 ---
        // 最初の背景からSpriteRendererを取得して、スプライトの幅を基準とします（全て同じ幅と仮定）
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

        // --- 背景を横一列に整列 ---
        // 各背景を隣り合わせにぴったり並べます
        for (int i = 0; i < backgrounds.Length; i++)
        {
            // ローカル座標で、(背景の幅 * インデックス番号) の位置に配置します
            backgrounds[i].transform.localPosition = new Vector3(backgroundWidth * i, 0, 0);

            // Rendererをキャッシュ
            Renderer renderer = backgrounds[i].GetComponent<Renderer>();
            if (renderer == null)
            {
                Debug.LogError($"背景オブジェクト '{backgrounds[i].name}' にRendererがありません。", this);
            }
            backgroundRenderers[i] = renderer;
        }

        // 親オブジェクトの初期位置を記録します
        lastParentPosition = transform.position;
    }

    /// <summary>
    /// このコンポーネントが有効になったときに呼び出される
    /// </summary>
    private void OnEnable()
    {
        // 有効化された瞬間の親の位置を記録し、意図しない移動差分の発生を防ぎます
        lastParentPosition = transform.position;
    }

    /// <summary>
    /// 全てのUpdate処理が終わった後に呼び出される更新処理
    /// </summary>
    void LateUpdate()
    {
        // 子オブジェクトのRendererのうち、どれか1つでもカメラに映っているか判定
        // (isVisible プロパティは、シーンビューもカメラとみなす点に注意)
        // .Any() はLINQの機能です
        bool isAnyVisible = backgroundRenderers.Any(r => r != null && r.isVisible);

        // 1つも見えていなければ、スクロール処理を行わない
        if (!isAnyVisible)
        {
            // ただし、親の位置は更新し続ける（次に見えた瞬間に座標がジャンプするのを防ぐため）
            lastParentPosition = transform.position;
            return;
        }

        // --- 1. 親の移動補正を行うか自動で判断 ---
        // scrollSpeedが閾値より大きい場合のみ、親の移動を打ち消す補正を有効にします。
        // scrollSpeedが0に近い場合は、親と一緒に動く静的な背景として扱われます。
        bool compensate = scrollSpeed >= compensationThreshold;

        // --- 2. このフレームでの背景の最終的な移動量を計算 ---
        float totalDeltaX;
        if (compensate)
        {
            // 【補正あり】親の動きを打ち消し、見た目のスクロール速度を一定に保ちます

            // a. 親がこのフレームでどれだけ移動したかを計算
            Vector3 parentDelta = transform.position - lastParentPosition;

            // b. 本来のスクロール量（左向きなのでマイナス）
            float baseScrollDeltaX = -scrollSpeed * Time.deltaTime;

            // c. 親のX軸移動を打ち消すための移動量
            float compensationDeltaX = -parentDelta.x;

            // d. 最終的な移動量は、本来のスクロール量と打ち消し分を足したもの
            totalDeltaX = baseScrollDeltaX + compensationDeltaX;
        }
        else
        {
            // 【補正なし】単純にscrollSpeedに従って移動します
            totalDeltaX = -scrollSpeed * Time.deltaTime;
        }

        // --- 3. 全ての背景を移動させる ---
        foreach (var bg in backgrounds)
        {
            // ローカル座標系で、計算された移動量だけ動かします
            bg.transform.localPosition += new Vector3(totalDeltaX, 0, 0);
        }

        // --- 4. 背景のループ処理 ---
        // 一番左にある背景オブジェクトを取得します
        GameObject firstBackground = backgrounds[0];
        // その背景が完全に画面外（左側）に出たか判定します
        if (firstBackground.transform.localPosition.x <= -backgroundWidth)
        {
            // 画面外に出た背景を、背景全体の合計幅の分だけ右にワープさせ、右端に繋げます
            Vector3 newPos = firstBackground.transform.localPosition;
            newPos.x += backgrounds.Length * backgroundWidth;
            firstBackground.transform.localPosition = newPos;

            // 配列の順序を更新し、次に左端に来る背景を新しい判定対象にします
            UpdateBackgroundsOrder();
        }

        // --- 5. 最後に現フレームの親の位置を記録 ---
        // 次のフレームで移動量を正しく計算するために、現在の位置を「前回の位置」として保存します
        lastParentPosition = transform.position;
    }

    /// <summary>
    /// 背景配列の順序を更新し、一番左にあった要素を一番右に移動させます。
    /// </summary>
    private void UpdateBackgroundsOrder()
    {
        // 0番目の要素（今、右端に移動したもの）を一時的に記憶
        GameObject first = backgrounds[0];

        // 配列の要素を1つずつ前方にずらす (1番目が0番目に、2番目が1番目に...)
        System.Array.Copy(backgrounds, 1, backgrounds, 0, backgrounds.Length - 1);

        // 記憶しておいた要素を配列の最後に設定
        backgrounds[backgrounds.Length - 1] = first;
    }
}
