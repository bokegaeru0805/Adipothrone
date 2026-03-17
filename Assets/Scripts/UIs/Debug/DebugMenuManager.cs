using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// デバッグ用のステータス変更・アイテム入手機能を提供するマネージャー。
/// </summary>
public class DebugMenuManager : MonoBehaviour
{
    [Header("UI参照")]
    [SerializeField]
    private Canvas debugCanvas; // デバッグメニュー全体をまとめたCanvas

    [Header("ステータス変更用入力")]
    [SerializeField]
    private TMP_InputField hpInput; // HP入力欄

    [SerializeField]
    private TMP_InputField wpInput; // WP入力欄

    [SerializeField]
    private TMP_InputField moneyInput; // 所持金入力欄

    [SerializeField]
    private TMP_InputField levelInput; // レベル指定用入力欄

    [Header("座標移動用入力")]
    [SerializeField]
    private TMP_InputField posInput; // X, Y座標をカンマ区切りで一気に入力するための入力欄

    [Header("アイテム入手設定")]
    [SerializeField]
    private TMP_InputField itemAmountInput; // 入手個数を指定する入力欄

    [Header("システム設定用入力")]
    [SerializeField]
    private TMP_InputField timeScaleInput; // ゲームスピード変更用入力欄

    public static bool isDebugModeUnlocked = false; // デバッグモードが解放されているか

    private void Start()
    {
        // PlayerPrefsからデバッグモードの解放状態を読み込む（1なら解放、0なら未解放）
        // エディタ上でもビルド後でも状態が保存・復元されるようになります
        isDebugModeUnlocked = PlayerPrefs.GetInt("DebugModeUnlocked", 0) == 1;

        if (debugCanvas != null)
        {
            debugCanvas.gameObject.SetActive(false);
        }

        if (hpInput != null)
            hpInput.onSubmit.AddListener(ApplyHP);
        if (wpInput != null)
            wpInput.onSubmit.AddListener(ApplyWP);
        if (moneyInput != null)
            moneyInput.onSubmit.AddListener(ApplyMoney);
        if (levelInput != null)
            levelInput.onSubmit.AddListener(ApplyLevel);

        // 座標入力欄でEnterが押されたときの処理を登録
        if (posInput != null)
            posInput.onSubmit.AddListener(ApplyPosition);

        // タイムスケール入力欄でEnterが押されたときの処理
        if (timeScaleInput != null)
        {
            timeScaleInput.onSubmit.AddListener(ApplyTimeScale);
        }

        // 初期値のセットアップ
        UpdateCurrentStatusToUI();
        if (itemAmountInput != null)
            itemAmountInput.text = "1"; // アイテム取得個数のデフォルト値
    }

    private void Update()
    {
        // デバッグモードが解放されていない場合は何もしない
        if (!isDebugModeUnlocked)
            return;

        if (Input.GetKeyDown(KeyCode.F2))
        {
            if (debugCanvas != null)
            {
                // 現在の状態の反転を取得し、GameObjectのアクティブ状態を切り替える
                bool nextState = !debugCanvas.gameObject.activeSelf;
                debugCanvas.gameObject.SetActive(nextState);

                // EventSystemが存在する場合、InputModuleを切り替える
                if (EventSystem.current != null)
                {
                    // アタッチされている2つのモジュールを取得
                    var standaloneModule =
                        EventSystem.current.GetComponent<StandaloneInputModule>();
                    var mouseOnlyModule = EventSystem.current.GetComponent<MouseOnlyInputModule>();

                    if (standaloneModule != null && mouseOnlyModule != null)
                    {
                        // デバッグ画面が開いているときは標準モジュールをON、カスタムモジュールをOFFにする
                        standaloneModule.enabled = debugCanvas.enabled;
                        mouseOnlyModule.enabled = !debugCanvas.enabled;
                    }
                }

                // デバッグ操作中にゲーム側のUIカーソルが勝手に動かないよう、UIEventNavigationHandlerも切り替える
                var customNav = FindObjectOfType<UIEventNavigationHandler>();
                if (customNav != null)
                {
                    customNav.enabled = !debugCanvas.enabled;
                }
            }
        }
    }

    /// <summary>
    /// HP入力欄でEnterが押されたときの処理
    /// </summary>
    private void ApplyHP(string text)
    {
        if (PlayerManager.instance == null)
            return;

        if (int.TryParse(text, out int hp))
        {
            // 最大HPの制限を無視してHPを強制設定する専用メソッドを呼び出す
            PlayerManager.instance.ForceSetHP(hp);
            Debug.Log($"HPを {hp} に強制設定しました。");
        }
    }

    /// <summary>
    /// WP入力欄でEnterが押されたときの処理
    /// </summary>
    private void ApplyWP(string text)
    {
        if (PlayerManager.instance == null)
            return;

        if (int.TryParse(text, out int wp))
        {
            PlayerManager.instance.SetWP(wp);
            Debug.Log($"WPを {wp} に変更しました。");
        }
    }

    /// <summary>
    /// 所持金入力欄でEnterが押されたときの処理
    /// </summary>
    private void ApplyMoney(string text)
    {
        if (PlayerManager.instance == null)
            return;

        if (int.TryParse(text, out int money))
        {
            // 直接代入するメソッドがないため、現在の所持金との差分を計算して増減させる
            int currentMoney = PlayerManager.instance.GetPlayerIntStatus(
                PlayerStatusIntName.playerMoney
            );
            int difference = money - currentMoney;
            PlayerManager.instance.ChangeMoney(difference);
            Debug.Log($"所持金を {money} に変更しました。");
        }
    }

    /// <summary>
    /// レベル指定用入力欄でEnterが押されたときの処理
    /// </summary>
    private void ApplyLevel(string text)
    {
        if (PlayerLevelManager.instance == null)
        {
            Debug.LogWarning("PlayerLevelManagerが存在しないため、レベルを変更できません。");
            return;
        }

        if (int.TryParse(text, out int targetLevel))
        {
            PlayerLevelManager.instance.SetPlayerLevel(targetLevel);
            Debug.Log($"レベルを {targetLevel} に変更しました。");
        }
    }

    /// <summary>
    /// 入力欄に入力されたアイテムの入手個数を取得します。不正な値の場合は最低 1 を返します。
    /// </summary>
    private int GetItemAmount()
    {
        if (itemAmountInput != null && int.TryParse(itemAmountInput.text, out int amount))
        {
            return Mathf.Max(1, amount); // 最低でも1個は入手するようにする
        }
        return 1;
    }

    /// <summary>
    /// すべての KeyItem を指定個数入手します。（UIボタンの OnClick 等に設定）
    /// </summary>
    public void GiveAllKeyItems()
    {
        if (GameManager.instance != null)
        {
            int amount = GetItemAmount();
            GameManager.instance.AddAllKeyItems(amount);
            Debug.Log($"すべての KeyItem を {amount} 個ずつ入手しました。");
        }
    }

    /// <summary>
    /// すべての HealItem を指定個数入手します。（UIボタンの OnClick 等に設定）
    /// </summary>
    public void GiveAllHealItems()
    {
        if (GameManager.instance != null)
        {
            int amount = GetItemAmount();
            GameManager.instance.AddAllHealItems(amount);
            Debug.Log($"すべての HealItem を {amount} 個ずつ入手しました。");
        }
    }

    /// <summary>
    /// すべての Weapon を指定個数入手します。（UIボタンの OnClick 等に設定）
    /// </summary>
    public void GiveAllWeapons()
    {
        if (WeaponManager.instance != null)
        {
            int amount = GetItemAmount();
            WeaponManager.instance.AddAllWeapons(amount);
            Debug.Log($"すべての Weapon を {amount} 個ずつ入手しました。");
        }
    }

    /// <summary>
    /// タイムスケール入力欄でEnterが押されたときの処理
    /// </summary>
    private void ApplyTimeScale(string text)
    {
        if (TimeManager.instance == null)
        {
            Debug.LogWarning("TimeManagerが存在しないため、ゲームスピードを変更できません。");
            return;
        }

        // 入力された文字列を小数 (float) に変換
        if (float.TryParse(text, out float scale))
        {
            TimeManager.instance.SetDebugTimeScale(scale);
            Debug.Log($"ゲームスピードを {scale} 倍に変更しました。");
        }
    }

    /// <summary>
    /// 座標入力欄でEnterが押されたときの処理。
    /// X, Yの形式で入力された文字列を解析してプレイヤーを一気に移動させます。
    /// </summary>
    private void ApplyPosition(string text)
    {
        if (PlayerManager.instance == null)
        {
            Debug.LogWarning("PlayerManagerが存在しないため、プレイヤーを移動できません。");
            return;
        }

        // 入力された文字列をカンマで分割する
        string[] splitText = text.Split(',');

        // カンマで区切られた2つの値が存在するか確認
        if (splitText.Length == 2)
        {
            // 余分な空白を取り除き、小数 (float) として解析する
            if (
                float.TryParse(splitText[0].Trim(), out float targetX)
                && float.TryParse(splitText[1].Trim(), out float targetY)
            )
            {
                Vector2 targetPos = new Vector2(targetX, targetY);
                // PlayerManagerの強制移動コルーチンを呼び出す
                PlayerManager.instance.StartCoroutine(PlayerManager.instance.PlayerMove(targetPos));
                Debug.Log($"プレイヤーを ({targetX}, {targetY}) に一気に移動させました。");
            }
            else
            {
                Debug.LogWarning("座標の数値解析に失敗しました。半角数字で入力してください。");
            }
        }
        else
        {
            Debug.LogWarning(
                "入力形式が正しくありません。「10.5, 20.0」のようにカンマで区切って入力してください。"
            );
        }
    }

    /// <summary>
    /// PlayerManagerやPlayerLevelManagerから現在のステータスを取得し、
    /// 各InputFieldのテキストにデフォルト値として反映させます。
    /// </summary>
    private void UpdateCurrentStatusToUI()
    {
        // PlayerManagerが存在する場合、HP・WP・所持金を取得
        if (PlayerManager.instance != null)
        {
            if (hpInput != null)
                hpInput.text = PlayerManager
                    .instance.GetPlayerIntStatus(PlayerStatusIntName.playerCurrentHP)
                    .ToString();

            if (wpInput != null)
                wpInput.text = PlayerManager
                    .instance.GetPlayerIntStatus(PlayerStatusIntName.playerCurrentWP)
                    .ToString();

            if (moneyInput != null)
                moneyInput.text = PlayerManager
                    .instance.GetPlayerIntStatus(PlayerStatusIntName.playerMoney)
                    .ToString();
        }

        // PlayerLevelManagerが存在する場合、レベルを取得
        if (PlayerLevelManager.instance != null && levelInput != null)
        {
            levelInput.text = PlayerLevelManager.instance.playerLv.ToString();
        }

        //プレイヤーの現在座標を取得し、X, Yの形式で表示
        if (PlayerManager.instance != null)
        {
            Vector2 currentPos = PlayerManager.instance.GetPlayerPosition();
            if (posInput != null)
            {
                // 小数点第2位まで表示し、間にカンマとスペースを入れる
                posInput.text = $"{currentPos.x:F2}, {currentPos.y:F2}";
            }
        }
        // 現在のゲームスピードを取得して表示
        if (TimeManager.instance != null && timeScaleInput != null)
        {
            timeScaleInput.text = TimeManager.instance.DebugBaseTimeScale.ToString("F1");
        }
    }
}
