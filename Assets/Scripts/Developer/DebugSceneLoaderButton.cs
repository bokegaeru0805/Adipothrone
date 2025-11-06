using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;

/// <summary>
/// キー入力またはクリックでシーンを切り替えるボタン。
/// WebGLでも遷移が分かるようフェード演出を行う。
/// </summary>
[RequireComponent(typeof(Button))]
public class DebugSceneLoaderButton : MonoBehaviour, IPointerDownHandler
{
    [Header("キー設定")]
    [SerializeField, Tooltip("このボタンに対応するキーボードのキー (長押し不可)")]
    private KeyCode targetKey = KeyCode.None;

    [Header("フェード設定")]
    [SerializeField, Tooltip("フェードイン/アウトにかける時間（秒）")]
    private float fadeDuration = 0.5f;
    [SerializeField, Tooltip("フェード時に表示するテキスト")]
    private string loadingText = "Now Loading...";
    [SerializeField, Tooltip("ローディングテキストの色")]
    private Color textColor = Color.white;

    // ★シーン名をインスペクターから設定できるように変更
    [Header("シーン設定")]
    [SerializeField, Tooltip("ロードするシーンの名前")]
    private string targetSceneName = "DebugScene";

    // --- 内部変数 ---
    private Button button;
    
    // ★staticに変更し、シーンをまたいで状態を共有する
    private static bool isLoading = false; 

    // --- フェード用UI (自動生成) ---
    private static Canvas fadeCanvas;
    private static Image fadeImage;
    private static Text fadeText;

    private void Awake()
    {
        button = GetComponent<Button>();
    }

    // ★★★ OnEnable, OnDisable, OnSceneLoaded は削除 ★★★


    // ★★★ Startメソッドを追加 ★★★
    /// <summary>
    /// シーン開始時に、もしフェードイン中（暗いまま）だったらフェードアウト（明るくする）を実行
    /// </summary>
    private void Start()
    {
        // このスクリプトがロード処理を開始した場合のみ、フェードアウトを実行
        if (isLoading)
        {
            // フェード用UIがなければ（＝シーンの初回起動時）作成
            if (fadeCanvas == null)
            {
                CreateFadeCanvas();
            }

            // フェードアウト処理を開始
            StartCoroutine(FadeOutAndReset());
        }
    }


    private void Update()
    {
        // isLoadingはstaticなので、どのボタンインスタンスもロード中は操作不可
        if (isLoading || button == null || !button.interactable)
        {
            return;
        }

        // キーが押された瞬間
        if (targetKey != KeyCode.None && Input.GetKeyDown(targetKey))
        {
            StartSceneLoad();
        }
    }

    // マウスでクリックされた瞬間に呼ばれる
    public void OnPointerDown(PointerEventData eventData)
    {
        if (isLoading || !button.interactable) return;
        StartSceneLoad();
    }

    /// <summary>
    /// シーン遷移のメイン処理を開始する
    /// </summary>
    private void StartSceneLoad()
    {
        if (isLoading) return;
        isLoading = true; // staticフラグを立てる

        Debug.Log($"シーン [{targetSceneName}] のロードを開始します。");
        StartCoroutine(FadeAndLoadScene());
    }

    /// <summary>
    /// フェードイン（画面を暗くする）とシーンロードを実行
    /// </summary>
    private IEnumerator FadeAndLoadScene()
    {
        // 1. フェードイン
        yield return StartCoroutine(Fade(1.0f)); // 1.0 = 真っ黒

        // 2. シーンロード（同期）
        // (isLoadingフラグがtrueのまま次のシーンがロードされる)
        SceneManager.LoadScene(targetSceneName); 
    }

    
    // ★★★ OnSceneLoaded は削除 ★★★


    /// <summary>
    /// フェードアウト（画面を明るくする）を実行し、フラグをリセット
    /// </summary>
    private IEnumerator FadeOutAndReset()
    {
        // 3. フェードアウト
        yield return StartCoroutine(Fade(0.0f)); // 0.0 = 透明
        
        // すべて完了したらstaticフラグをリセット
        isLoading = false; 
    }

    /// <summary>
    /// フェード処理を行うコルーチン
    /// </summary>
    private IEnumerator Fade(float targetAlpha)
    {
        // フェード用UIがなければ自動生成
        if (fadeCanvas == null)
        {
            CreateFadeCanvas();
        }

        fadeCanvas.gameObject.SetActive(true);
        fadeText.text = (targetAlpha == 1.0f) ? loadingText : ""; // 暗くするときだけテキスト表示

        float startAlpha = fadeImage.color.a;
        float timer = 0f;

        while (timer < fadeDuration)
        {
            timer += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(timer / fadeDuration);
            float currentAlpha = Mathf.Lerp(startAlpha, targetAlpha, progress);

            fadeImage.color = new Color(0f, 0f, 0f, currentAlpha);
            fadeText.color = new Color(textColor.r, textColor.g, textColor.b, currentAlpha);

            yield return null;
        }

        fadeImage.color = new Color(0f, 0f, 0f, targetAlpha);
        fadeText.color = new Color(textColor.r, textColor.g, textColor.b, targetAlpha);

        // 透明になったらCanvasを非表示にする
        if (targetAlpha == 0.0f)
        {
            fadeCanvas.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// WebGLビルドでも最前面に表示されるフェード用Canvasを自動生成する
    /// </summary>
    private void CreateFadeCanvas()
    {
        // このCanvasはシーンをまたいで存在させる
        GameObject canvasObj = new GameObject("DebugFadeCanvas");
        fadeCanvas = canvasObj.AddComponent<Canvas>();
        fadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        fadeCanvas.sortingOrder = 9999; // 常に最前面
        canvasObj.AddComponent<CanvasScaler>();
        Object.DontDestroyOnLoad(canvasObj); // ★シーンをまたいで存在させる

        // 背景Image
        GameObject imageObj = new GameObject("FadeImage");
        imageObj.transform.SetParent(canvasObj.transform, false);
        fadeImage = imageObj.AddComponent<Image>();
        fadeImage.color = new Color(0f, 0f, 0f, 0f); // 初期は透明
        fadeImage.rectTransform.anchorMin = Vector2.zero;
        fadeImage.rectTransform.anchorMax = Vector2.one;
        fadeImage.rectTransform.sizeDelta = Vector2.zero;

        // ローディングText
        GameObject textObj = new GameObject("FadeText");
        textObj.transform.SetParent(canvasObj.transform, false);
        fadeText = textObj.AddComponent<Text>();
        fadeText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); // デフォルトフォント
        fadeText.fontSize = 32;
        fadeText.color = new Color(textColor.r, textColor.g, textColor.b, 0f); // 初期は透明
        fadeText.alignment = TextAnchor.MiddleCenter;
        fadeText.rectTransform.anchorMin = Vector2.zero;
        fadeText.rectTransform.anchorMax = Vector2.one;
        fadeText.rectTransform.sizeDelta = Vector2.zero;
    }
}