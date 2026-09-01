using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

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

    [Header("マスターデータ")]
    [SerializeField]
    private EnemyDatabase enemyDatabase; // 全敵のドロップ情報解放に使用する敵データベース

    [Header("システム設定用入力")]
    [SerializeField]
    private TMP_InputField timeScaleInput; // ゲームスピード変更用入力欄

    [Header("戦闘テスト設定")]
    [SerializeField]
    private bool isMouseDamageEnabled;

    [SerializeField, Range(0f, 100f)]
    private float mouseDamagePercent = 25f;

    [SerializeField]
    private bool isPlayerInvincible;

    [Header("デバッグ表示設定")]
    [SerializeField]
    private Toggle eventAreaToggle; // イベントエリアの表示・非表示を切り替えるトグル

    [Header("実行時生成UI")]
    [SerializeField, Tooltip("未指定の場合はTextMeshProのDefault Font Assetを使用します。")]
    private TMP_FontAsset debugFont;

    public static bool isDebugModeUnlocked = false; // デバッグモードが解放されているか
    public static bool isShowEventArea { get; private set; } = false; // イベントエリアの表示・非表示状態
    public static System.Action<bool> OnEventAreaDisplayToggled; // イベントエリアの表示・非表示が切り替わったときに呼び出されるイベント

    private DebugMenuUIBuilder.View _view;
    private bool _isMenuOpen;
    private StandaloneInputModule _standaloneInputModule;
    private MouseOnlyInputModule _mouseOnlyInputModule;
    private UIEventNavigationHandler _customNavigation;
    private bool _wasStandaloneInputEnabled;
    private bool _wasMouseOnlyInputEnabled;
    private bool _wasCustomNavigationEnabled;
    private GameObject _previousSelectedObject;
    private float _fpsElapsed;
    private int _fpsFrameCount;
    private Heroin_move _playerController;

    private void Awake()
    {
        // 他のスクリプトのStart()よりも先に実行させるため、Awakeで読み込む

        // PlayerPrefsからデバッグモードの解放状態を読み込む（1なら解放、0なら未解放）
        // エディタ上でもビルド後でも状態が保存・復元されるようになります
        isDebugModeUnlocked = PlayerPrefs.GetInt("DebugModeUnlocked", 0) == 1;

        // Scene/Prefab上の手動構築UIには依存せず、実行時に新しいUIを生成する。
        Transform uiParent = transform.parent != null ? transform.parent : transform;
        TMP_FontAsset runtimeFont = debugFont;
        if (runtimeFont == null && hpInput != null && hpInput.textComponent != null)
            runtimeFont = hpInput.textComponent.font;
        _view = new DebugMenuUIBuilder(uiParent, runtimeFont).Build();
        hpInput = _view.HpInput;
        wpInput = _view.WpInput;
        moneyInput = _view.MoneyInput;
        levelInput = _view.LevelInput;
        posInput = _view.PositionInput;
        itemAmountInput = _view.ItemAmountInput;
        timeScaleInput = _view.TimeScaleInput;
        eventAreaToggle = _view.EventAreaToggle;
    }

    private void Start()
    {
        LoadDebugSettings();

        // 旧DebugCanvasは移行期間中もPrefabに残すが、表示には使用しない。
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

        // トグルの初期化とイベント登録
        if (eventAreaToggle != null)
        {
            // セーブデータや初期状態に合わせてトグルの見た目を同期
            eventAreaToggle.SetIsOnWithoutNotify(isShowEventArea);
            // トグルの値が変更された時に実行するメソッドを登録
            eventAreaToggle.onValueChanged.AddListener(OnToggleEventArea);
        }

        _view.MouseDamageToggle.SetIsOnWithoutNotify(isMouseDamageEnabled);
        _view.PlayerInvincibleToggle.SetIsOnWithoutNotify(isPlayerInvincible);
        _view.MouseDamagePercentInput.text = mouseDamagePercent.ToString("0.##");
        _view.MouseDamageToggle.onValueChanged.AddListener(OnToggleMouseDamage);
        _view.PlayerInvincibleToggle.onValueChanged.AddListener(OnTogglePlayerInvincible);
        _view.MouseDamagePercentInput.onSubmit.AddListener(ApplyMouseDamagePercent);
        ApplyPlayerInvincibility();

        // 初期値のセットアップ
        UpdateCurrentStatusToUI();
        if (itemAmountInput != null)
            itemAmountInput.text = "1"; // アイテム取得個数のデフォルト値

        BindGeneratedUIEvents();
        UpdateRuntimeInformation();
    }

    private void Update()
    {
        // デバッグモードが解放されていない場合は何もしない
        if (!isDebugModeUnlocked)
            return;

        if (Input.GetKeyDown(KeyCode.F2))
        {
            SetMenuOpen(!_isMenuOpen);
        }

        if (_isMenuOpen && Input.GetKeyDown(KeyCode.Escape))
            SetMenuOpen(false);

        if (_isMenuOpen)
            UpdateFpsDisplay();
        else
            ApplyMouseDamageOnClick();

        if (isPlayerInvincible)
            ApplyPlayerInvincibility();
    }

    private void OnDisable()
    {
        if (_isMenuOpen)
            SetMenuOpen(false);
    }

    /// <summary>
    /// 実行時生成したSelectableへDebugMenuManagerの処理を接続します。
    /// </summary>
    private void BindGeneratedUIEvents()
    {
        _view.CloseButton.onClick.AddListener(() => SetMenuOpen(false));
        _view.RefreshButton.onClick.AddListener(UpdateCurrentStatusToUI);
        _view.ApplyHpButton.onClick.AddListener(() => ApplyHP(hpInput.text));
        _view.ApplyWpButton.onClick.AddListener(() => ApplyWP(wpInput.text));
        _view.ApplyMoneyButton.onClick.AddListener(() => ApplyMoney(moneyInput.text));
        _view.ApplyLevelButton.onClick.AddListener(() => ApplyLevel(levelInput.text));
        _view.ApplyPositionButton.onClick.AddListener(() => ApplyPosition(posInput.text));
        _view.GiveAllKeyItemsButton.onClick.AddListener(GiveAllKeyItems);
        _view.GiveAllHealItemsButton.onClick.AddListener(GiveAllHealItems);
        _view.GiveAllStatusEnhanceItemsButton.onClick.AddListener(GiveAllStatusEnhanceItems);
        _view.GiveAllMaterialItemsButton.onClick.AddListener(GiveAllMaterialItems);
        _view.GiveAllWeaponsButton.onClick.AddListener(GiveAllWeapons);
        _view.GiveAllRecipeItemsButton.onClick.AddListener(GiveAllRecipeItems);
        _view.UnlockAllSkillsButton.onClick.AddListener(UnlockAllSkills);
        _view.UnlockAllEnemyDropItemsButton.onClick.AddListener(UnlockAllEnemyDropItems);
        _view.ApplyTimeScaleButton.onClick.AddListener(() => ApplyTimeScale(timeScaleInput.text));
        _view.ApplyMouseDamagePercentButton.onClick.AddListener(() => ApplyMouseDamagePercent(_view.MouseDamagePercentInput.text));
        _view.ResetDebugSettingsButton.onClick.AddListener(ResetDebugSettings);

        float[] presets = { 0.25f, 0.5f, 1f, 2f, 4f };
        for (int i = 0; i < _view.TimeScalePresetButtons.Count; i++)
        {
            float preset = presets[i];
            _view.TimeScalePresetButtons[i].onClick.AddListener(() =>
            {
                timeScaleInput.text = preset.ToString("0.##");
                ApplyTimeScale(timeScaleInput.text);
            });
        }
    }

    private void SetMenuOpen(bool isOpen)
    {
        if (_view == null || _isMenuOpen == isOpen)
            return;

        _isMenuOpen = isOpen;
        if (isOpen)
        {
            CacheAndOverrideInputState();
            UpdateCurrentStatusToUI();
            UpdateRuntimeInformation();
            SetStatus("準備完了", false);
            _view.Root.SetActive(true);
            DebugMenuUIBuilder.SelectTab(_view, 0);
        }
        else
        {
            _view.Root.SetActive(false);
            RestoreInputState();
        }
    }

    private void CacheAndOverrideInputState()
    {
        if (EventSystem.current != null)
        {
            _previousSelectedObject = EventSystem.current.currentSelectedGameObject;
            _standaloneInputModule = EventSystem.current.GetComponent<StandaloneInputModule>();
            _mouseOnlyInputModule = EventSystem.current.GetComponent<MouseOnlyInputModule>();
        }

        _customNavigation = FindObjectOfType<UIEventNavigationHandler>();
        if (_standaloneInputModule != null)
        {
            _wasStandaloneInputEnabled = _standaloneInputModule.enabled;
            _standaloneInputModule.enabled = true;
        }
        if (_mouseOnlyInputModule != null)
        {
            _wasMouseOnlyInputEnabled = _mouseOnlyInputModule.enabled;
            _mouseOnlyInputModule.enabled = false;
        }
        if (_customNavigation != null)
        {
            _wasCustomNavigationEnabled = _customNavigation.enabled;
            _customNavigation.enabled = false;
        }
    }

    private void RestoreInputState()
    {
        if (_standaloneInputModule != null)
            _standaloneInputModule.enabled = _wasStandaloneInputEnabled;
        if (_mouseOnlyInputModule != null)
            _mouseOnlyInputModule.enabled = _wasMouseOnlyInputEnabled;
        if (_customNavigation != null)
            _customNavigation.enabled = _wasCustomNavigationEnabled;

        if (EventSystem.current != null)
        {
            GameObject selection =
                _previousSelectedObject != null && _previousSelectedObject.activeInHierarchy
                    ? _previousSelectedObject
                    : null;
            EventSystem.current.SetSelectedGameObject(selection);
        }
    }

    private void UpdateRuntimeInformation()
    {
        if (_view == null)
            return;
        _view.SceneText.text = $"シーン: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}";
        _fpsElapsed = 0f;
        _fpsFrameCount = 0;
    }

    private void UpdateFpsDisplay()
    {
        _fpsElapsed += Time.unscaledDeltaTime;
        _fpsFrameCount++;
        if (_fpsElapsed < 0.5f)
            return;

        float fps = _fpsElapsed > 0f ? _fpsFrameCount / _fpsElapsed : 0f;
        _view.FpsText.text = $"FPS: {fps:0}";
        _fpsElapsed = 0f;
        _fpsFrameCount = 0;
    }

    private void SetStatus(string message, bool isError)
    {
        if (_view == null || _view.StatusText == null)
            return;
        _view.StatusText.text = message;
        _view.StatusText.color = isError
            ? new Color32(255, 103, 103, 255)
            : new Color32(105, 224, 151, 255);
    }

    /// <summary>
    /// HP入力欄でEnterが押されたときの処理
    /// </summary>
    private void ApplyHP(string text)
    {
        if (PlayerManager.instance == null)
        {
            SetStatus("エラー: PlayerManagerが存在しません。", true);
            return;
        }

        if (int.TryParse(text, out int hp))
        {
            // 最大HPの制限を無視してHPを強制設定する専用メソッドを呼び出す
            PlayerManager.instance.ForceSetHP(hp);
            Debug.Log($"HPを {hp} に強制設定しました。");
            SetStatus($"HPを {hp} に変更しました。", false);
        }
        else
            SetStatus("エラー: HPには整数を入力してください。", true);
    }

    /// <summary>
    /// WP入力欄でEnterが押されたときの処理
    /// </summary>
    private void ApplyWP(string text)
    {
        if (PlayerManager.instance == null)
        {
            SetStatus("エラー: PlayerManagerが存在しません。", true);
            return;
        }

        if (int.TryParse(text, out int wp))
        {
            PlayerManager.instance.SetWP(wp);
            Debug.Log($"WPを {wp} に変更しました。");
            SetStatus($"WPを {wp} に変更しました。", false);
        }
        else
            SetStatus("エラー: WPには整数を入力してください。", true);
    }

    /// <summary>
    /// 所持金入力欄でEnterが押されたときの処理
    /// </summary>
    private void ApplyMoney(string text)
    {
        if (PlayerManager.instance == null)
        {
            SetStatus("エラー: PlayerManagerが存在しません。", true);
            return;
        }

        if (int.TryParse(text, out int money))
        {
            // 直接代入するメソッドがないため、現在の所持金との差分を計算して増減させる
            int currentMoney = PlayerManager.instance.GetPlayerIntStatus(
                PlayerStatusIntName.playerMoney
            );
            int difference = money - currentMoney;
            PlayerManager.instance.ChangeMoney(difference);
            Debug.Log($"所持金を {money} に変更しました。");
            SetStatus($"所持金を {money} に変更しました。", false);
        }
        else
            SetStatus("エラー: 所持金には整数を入力してください。", true);
    }

    /// <summary>
    /// レベル指定用入力欄でEnterが押されたときの処理
    /// </summary>
    private void ApplyLevel(string text)
    {
        if (PlayerLevelManager.instance == null)
        {
            Debug.LogWarning("PlayerLevelManagerが存在しないため、レベルを変更できません。");
            SetStatus("エラー: PlayerLevelManagerが存在しません。", true);
            return;
        }

        if (int.TryParse(text, out int targetLevel))
        {
            PlayerLevelManager.instance.SetPlayerLevel(targetLevel);
            Debug.Log($"レベルを {targetLevel} に変更しました。");
            SetStatus($"レベルを {targetLevel} に変更しました。", false);
        }
        else
            SetStatus("エラー: レベルには整数を入力してください。", true);
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
            SetStatus($"全キーアイテムを {amount} 個ずつ付与しました。", false);
        }
        else
            SetStatus("エラー: GameManagerが存在しません。", true);
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
            SetStatus($"全回復アイテムを {amount} 個ずつ付与しました。", false);
        }
        else
            SetStatus("エラー: GameManagerが存在しません。", true);
    }

    /// <summary>
    /// すべての StatusEnhanceItem を指定個数入手します。（UIボタンの OnClick 等に設定）
    /// </summary>
    public void GiveAllStatusEnhanceItems()
    {
        if (GameManager.instance != null)
        {
            int amount = GetItemAmount();
            GameManager.instance.AddAllStatusEnhanceItems(amount);
            Debug.Log($"すべての StatusEnhanceItem を {amount} 個ずつ入手しました。");
            SetStatus($"全強化アイテムを {amount} 個ずつ付与しました。", false);
        }
        else
            SetStatus("エラー: GameManagerが存在しません。", true);
    }

    /// <summary>
    /// すべての MaterialItem を指定個数入手します。（UIボタンの OnClick 等に設定）
    /// </summary>
    public void GiveAllMaterialItems()
    {
        if (GameManager.instance != null)
        {
            int amount = GetItemAmount();
            GameManager.instance.AddAllMaterialItems(amount);
            Debug.Log($"すべての MaterialItem を {amount} 個ずつ入手しました。");
            SetStatus($"全素材アイテムを {amount} 個ずつ付与しました。", false);
        }
        else
            SetStatus("エラー: GameManagerが存在しません。", true);
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
            SetStatus($"全武器を {amount} 個ずつ付与しました。", false);
        }
        else
            SetStatus("エラー: WeaponManagerが存在しません。", true);
    }

    /// <summary>
    /// すべての RecipeItem を指定個数入手します。（UIボタンの OnClick 等に設定）
    /// </summary>
    public void GiveAllRecipeItems()
    {
        if (GameManager.instance != null)
        {
            int amount = GetItemAmount();
            GameManager.instance.AddAllRecipeItems(amount);
            Debug.Log($"すべての RecipeItem を {amount} 個ずつ入手しました。");
            SetStatus($"全レシピを {amount} 個ずつ付与しました。", false);
        }
        else
            SetStatus("エラー: GameManagerが存在しません。", true);
    }

    /// <summary>
    /// すべてのスキル(Skill)を解放します。（UIボタンの OnClick 等に設定）
    /// </summary>
    public void UnlockAllSkills()
    {
        if (GameManager.instance != null)
        {
            GameManager.instance.UnlockAllSkills();
            Debug.Log("すべての Skill を解放しました。");
            SetStatus("全スキルを解放しました。", false);
        }
        else
            SetStatus("エラー: GameManagerが存在しません。", true);
    }

    /// <summary>
    /// EnemyDatabaseに登録されている全敵を討伐済みにし、ドロップアイテム情報を解放します。
    /// </summary>
    public void UnlockAllEnemyDropItems()
    {
        if (GameManager.instance == null || GameManager.instance.savedata == null)
        {
            SetStatus("エラー: セーブデータが存在しません。", true);
            return;
        }

        if (enemyDatabase == null || enemyDatabase.enemies == null)
        {
            Debug.LogWarning("EnemyDatabaseが設定されていないため、ドロップ情報を解放できません。");
            SetStatus("エラー: EnemyDatabaseが設定されていません。", true);
            return;
        }

        if (GameManager.instance.savedata.EnemyRecordData == null)
        {
            GameManager.instance.savedata.EnemyRecordData = new EnemyRecordData();
        }

        int unlockedEnemyCount = 0;
        foreach (EnemyData enemyData in enemyDatabase.enemies)
        {
            if (enemyData == null)
                continue;

            // 図鑑で敵の全情報を表示できるよう、未討伐の敵だけ討伐数を1にする。
            if (GameManager.instance.savedata.EnemyRecordData.GetKillCount(enemyData.enemyID) <= 0)
            {
                GameManager.instance.savedata.EnemyRecordData.AddKillCount(enemyData.enemyID);
            }

            GameManager.instance.savedata.EnemyRecordData.UnlockAllDropItems(enemyData);
            unlockedEnemyCount++;
        }

        Debug.Log($"{unlockedEnemyCount}体の敵情報とドロップ情報をすべて解放しました。");
        SetStatus($"全敵情報・ドロップを解放しました（{unlockedEnemyCount}体）。", false);
    }

    /// <summary>
    /// タイムスケール入力欄でEnterが押されたときの処理
    /// </summary>
    private void ApplyTimeScale(string text)
    {
        if (TimeManager.instance == null)
        {
            Debug.LogWarning("TimeManagerが存在しないため、ゲームスピードを変更できません。");
            SetStatus("エラー: TimeManagerが存在しません。", true);
            return;
        }

        // 入力された文字列を小数 (float) に変換
        if (float.TryParse(text, out float scale))
        {
            TimeManager.instance.SetDebugTimeScale(scale);
            Debug.Log($"ゲームスピードを {scale} 倍に変更しました。");
            timeScaleInput.text = TimeManager.instance.DebugBaseTimeScale.ToString("0.##");
            SaveDebugSettings();
            SetStatus($"ゲーム速度を {TimeManager.instance.DebugBaseTimeScale:0.##} 倍に変更しました。", false);
        }
        else
            SetStatus("エラー: ゲーム速度には数値を入力してください。", true);
    }

    /// <summary>
    /// マウスクリック時に、クリック位置の敵または破壊可能オブジェクトへ割合ダメージを与えます。
    /// </summary>
    private void ApplyMouseDamageOnClick()
    {
        if (!isMouseDamageEnabled || !Input.GetMouseButtonDown(0))
            return;

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogWarning("メインカメラが存在しないため、マウスダメージを適用できません。");
            return;
        }

        float damageRatio = mouseDamagePercent / 100f;
        if (damageRatio <= 0f)
            return;

        Vector2 mouseWorldPosition = mainCamera.ScreenToWorldPoint(Input.mousePosition);
        Collider2D[] hitColliders = Physics2D.OverlapPointAll(mouseWorldPosition);
        var damagedTargets = new HashSet<CharacterHealth>();

        foreach (Collider2D hitCollider in hitColliders)
        {
            CharacterHealth health = hitCollider.GetComponentInParent<CharacterHealth>();
            if (health == null || !damagedTargets.Add(health) || health.MaxHP <= 0)
                continue;

            int damage = Mathf.CeilToInt(health.MaxHP * damageRatio);
            health.Damage(damage);
        }
    }

    private void ApplyMouseDamagePercent(string text)
    {
        if (!float.TryParse(text, out float percent))
        {
            SetStatus("エラー: ダメージ率には数値を入力してください。", true);
            return;
        }

        mouseDamagePercent = Mathf.Clamp(percent, 0f, 100f);
        _view.MouseDamagePercentInput.text = mouseDamagePercent.ToString("0.##");
        SaveDebugSettings();
        SetStatus($"クリックダメージを最大HPの{mouseDamagePercent:0.##}%に設定しました。", false);
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
            SetStatus("エラー: PlayerManagerが存在しません。", true);
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
                SetStatus($"座標 ({targetX:0.##}, {targetY:0.##}) へ移動しました。", false);
            }
            else
            {
                Debug.LogWarning("座標の数値解析に失敗しました。半角数字で入力してください。");
                SetStatus("エラー: 座標には数値を入力してください。", true);
            }
        }
        else
        {
            Debug.LogWarning(
                "入力形式が正しくありません。「10.5, 20.0」のようにカンマで区切って入力してください。"
            );
            SetStatus("エラー: 座標は「X, Y」の形式で入力してください。", true);
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

        SetStatus("現在値を更新しました。", false);
    }

    /// <summary>
    /// イベントエリアの表示・非表示を切り替え、各イベントに通知します。
    /// </summary>
    private void OnToggleEventArea(bool value)
    {
        isShowEventArea = value;
        SaveDebugSettings();

        OnEventAreaDisplayToggled?.Invoke(value);
        Debug.Log($"イベントエリア表示を {(value ? "ON" : "OFF")} に切り替え、保存しました。");
    }

    private void OnToggleMouseDamage(bool value)
    {
        isMouseDamageEnabled = value;
        SaveDebugSettings();
        SetStatus($"クリックダメージを{(value ? "有効" : "無効")}にしました。", false);
    }

    private void OnTogglePlayerInvincible(bool value)
    {
        isPlayerInvincible = value;
        ApplyPlayerInvincibility();
        SaveDebugSettings();
        SetStatus($"プレイヤー無敵を{(value ? "有効" : "無効")}にしました。", false);
    }

    private void ApplyPlayerInvincibility()
    {
        if (_playerController == null)
            _playerController = FindObjectOfType<Heroin_move>();

        if (_playerController != null)
            _playerController.SetDebugInvincibility(isPlayerInvincible);
    }

    private void LoadDebugSettings()
    {
        DebugSettingsSaveData settings = SaveLoadManager.instance?.DebugSettings;
        if (settings == null)
            return;

        settings.Validate();
        isShowEventArea = settings.isShowEventArea;
        isMouseDamageEnabled = settings.isMouseDamageEnabled;
        mouseDamagePercent = settings.mouseDamagePercent;
        isPlayerInvincible = settings.isPlayerInvincible;
        OnEventAreaDisplayToggled?.Invoke(isShowEventArea);

        if (TimeManager.instance != null)
            TimeManager.instance.SetDebugTimeScale(settings.debugTimeScale);
    }

    private void SaveDebugSettings()
    {
        SaveLoadManager saveLoadManager = SaveLoadManager.instance;
        if (saveLoadManager == null || saveLoadManager.DebugSettings == null)
            return;

        DebugSettingsSaveData settings = saveLoadManager.DebugSettings;
        settings.isShowEventArea = isShowEventArea;
        settings.isMouseDamageEnabled = isMouseDamageEnabled;
        settings.mouseDamagePercent = mouseDamagePercent;
        settings.isPlayerInvincible = isPlayerInvincible;
        if (TimeManager.instance != null)
            settings.debugTimeScale = TimeManager.instance.DebugBaseTimeScale;

        saveLoadManager.SaveDebugSettings();
    }

    private void ResetDebugSettings()
    {
        var defaults = new DebugSettingsSaveData();
        isShowEventArea = defaults.isShowEventArea;
        isMouseDamageEnabled = defaults.isMouseDamageEnabled;
        mouseDamagePercent = defaults.mouseDamagePercent;
        isPlayerInvincible = defaults.isPlayerInvincible;

        eventAreaToggle.SetIsOnWithoutNotify(isShowEventArea);
        _view.MouseDamageToggle.SetIsOnWithoutNotify(isMouseDamageEnabled);
        _view.PlayerInvincibleToggle.SetIsOnWithoutNotify(isPlayerInvincible);
        _view.MouseDamagePercentInput.text = mouseDamagePercent.ToString("0.##");

        if (TimeManager.instance != null)
        {
            TimeManager.instance.SetDebugTimeScale(defaults.debugTimeScale);
            timeScaleInput.text = TimeManager.instance.DebugBaseTimeScale.ToString("0.##");
        }

        OnEventAreaDisplayToggled?.Invoke(isShowEventArea);
        ApplyPlayerInvincibility();
        SaveDebugSettings();
        SetStatus("デバッグ設定を初期値へ戻しました。", false);
    }
}
