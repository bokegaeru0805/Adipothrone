using System;
using System.Collections;
using System.Collections.Generic;
using Fungus;
using TMPro;
using UnityEngine;

/// <summary>
/// ゲーム内の主要なUI（プレイヤーHP/WP、ボスHP、アイテムログ、レベルアップ通知など）を一括管理するマネージャクラス。
/// </summary>
public class GameUIManager : MonoBehaviour
{
    #region Singleton & Properties

    public static GameUIManager instance { get; private set; }

    /// <summary>
    /// 現在ボスとの戦闘中か（ボスUIが表示されているか）どうか。
    /// </summary>
    public bool IsInBossBattle { get; private set; } = false;

    /// <summary>
    /// ボス戦の状態が変更されたときに発行されるイベント
    /// (true = ボス戦開始, false = ボス戦終了)
    /// </summary>
    public static event Action<bool> OnBossBattleStateChanged;

    #endregion

    #region Inspector Settings & References

    [Header("UI参照のルート")]
    [SerializeField]
    private GameUIRefs _uiRefs = null;

    [Header("UI表示設定")]
    [SerializeField, Tooltip("アイテム獲得UIの表示時間（秒）")]
    private float _itemDisplayDuration = 5f;

    [SerializeField, Tooltip("レベルアップポップアップの表示時間（秒）")]
    private float _levelUpDisplayTime = 3f;

    [SerializeField, Tooltip("技名表示の表示時間（秒）")]
    private float _skillNameDisplayTime = 3f;

    #endregion

    #region Private Variables

    // --- 外部参照 ---
    private PlayerManager _playerManager;

    // --- プレイヤー状態キャッシュ ---
    private int _currentPlayerHP;
    private int _playerMaxHP;
    private int _currentPlayerWP;
    private int _playerMaxWP;

    // --- ボス状態キャッシュ ---
    private GameObject _currentBossGameObject = null;
    private CharacterHealth _currentBossHealthScript = null; // 現在のボスHPスクリプト
    private int _currentBossHP;
    private int _bossMaxHP;

    // --- UIアニメーション用変数 (SmoothDamp) ---
    private float _maxSpeed = float.PositiveInfinity; // 最高速度
    private float _playerHPBarVelocity = 0f;
    private float _playerWPBarVelocity = 0f;
    private float _bossHPBarVelocity = 0f;

    // --- アイテムログ管理 ---
    private class ItemInfo
    {
        public string itemName;
        public float timestamp;
    }

    private Queue<ItemInfo> _itemLogQueue = new Queue<ItemInfo>();

    // --- 状態フラグ ---
    private bool _isTalking = false; // 会話状態を保存するローカル変数
    private bool _isStoryTalking = false; // Storyブロックの会話状態を保存するローカル変数
    #endregion

    #region Unity Lifecycle Methods

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            SetBossBattleState(false); // 起動時は必ずfalse

            CheckAndInitReferences();
            HideInitialUI();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // StartでPlayerManagerのインスタンスを一度だけ取得し、保持する
        _playerManager = PlayerManager.instance;
        if (_playerManager == null)
        {
            Debug.LogError(
                "PlayerManagerのインスタンスが見つかりません！このスクリプトは動作しません。"
            );
            return; // PlayerManagerがなければ、ここで処理を中断
        }

        // イベント購読と初期値の取得
        if (_playerManager != null)
        {
            _playerManager.OnChangeHP += UpdatePlayerHPCache;
            _playerManager.OnChangeWP += UpdatePlayerWPCache;
            _playerManager.OnChangeMaxHP += InitializePlayerHPData;

            InitializePlayerHPData(_playerManager.playerMaxHP); //プレイヤーのHPの初期値を取得
            UpdatePlayerWPCache(
                _playerManager.GetPlayerIntStatus(PlayerStatusIntName.playerCurrentWP)
            ); // プレイヤーのWPの初期値を取得
        }

        GameManager.OnTalkingStateChanged += HandleTalkingStateChanged; // 会話状態の変更に応じてボスのUIをアクティブにする

        InitializePlayerWPData(); // プレイヤーのWPの初期値を取得
    }

    private void Update()
    {
        // 各種UIの更新処理
        UpdatePlayerUI();
        UpdateItemLogUI();
        UpdateBossUI();
    }

    private void OnDisable()
    {
        // オブジェクトが破棄される際などにも呼ばれるため、playerManagerが存在するか確認
        if (_playerManager != null)
        {
            _playerManager.OnChangeHP -= UpdatePlayerHPCache;
            _playerManager.OnChangeWP -= UpdatePlayerWPCache;
            _playerManager.OnChangeMaxHP -= InitializePlayerHPData;
        }

        GameManager.OnTalkingStateChanged -= HandleTalkingStateChanged;
    }

    #endregion

    #region Initialization Helpers

    /// <summary>
    /// 必要なUI参照が設定されているか確認し、エラーログを出力します。
    /// </summary>
    private void CheckAndInitReferences()
    {
        if (_uiRefs == null)
        {
            Debug.LogError("GameUIManagerにGameUIRefsが設定されていません！");
            return;
        }

        if (
            _uiRefs.BossHealthBarImage == null
            || _uiRefs.BossHealthUIPanel == null
            || _uiRefs.BossLevelNumberText == null
        )
        {
            Debug.LogError(
                "GameUIRefsにボスのHPバー、背景、レベルUI、レベル番号テキストが設定されていません"
            );
        }

        if (_uiRefs.SkillNameDisplay == null)
        {
            Debug.LogError("GameUIRefsに技名表示のUIが設定されていません");
        }

        if (_uiRefs.FastTravelPanel == null)
        {
            Debug.LogError("GameUIRefsにファストトラベルのパネルUIが設定されていません");
        }
    }

    /// <summary>
    /// ゲーム開始時に非表示にしておくべきUIを隠します。
    /// </summary>
    private void HideInitialUI()
    {
        if (_uiRefs == null)
            return;

        // ボスUI非表示
        SetBossUIVisibility(false);
        if (_uiRefs.BossLevelNumberText != null)
        {
            _uiRefs.BossLevelNumberText.text = "???";
        }

        // 技名UI非表示
        if (_uiRefs.SkillNameDisplay != null)
        {
            _uiRefs.SkillNameDisplay.SetActive(false);
        }

        // ファストトラベルUI非表示
        if (_uiRefs.FastTravelPanel != null)
        {
            _uiRefs.FastTravelPanel.SetActive(false);
        }

        // アイテムログUI非表示
        foreach (var slot in _uiRefs.ItemLogSlots)
        {
            if (slot != null)
                slot.SetActive(false);
        }

        // レベルアップポップアップ非表示
        if (_uiRefs.LevelUpPopup != null)
        {
            _uiRefs.LevelUpPopup.SetActive(false);
        }
    }

    #endregion

    #region Update Loop Logic

    /// <summary>
    /// プレイヤーのHP/WPバーを滑らかに更新します。
    /// </summary>
    private void UpdatePlayerUI()
    {
        // HPバーの更新
        if (_uiRefs.PlayerHPHealthBarImage != null && _playerMaxHP > 0)
        {
            _uiRefs.PlayerHPHealthBarImage.fillAmount = Mathf.SmoothDamp(
                _uiRefs.PlayerHPHealthBarImage.fillAmount,
                (float)_currentPlayerHP / (float)_playerMaxHP,
                ref _playerHPBarVelocity,
                GameConstants.GAUGE_SMOOTH_TIME,
                _maxSpeed,
                Time.unscaledDeltaTime
            );
        }

        // WPバーの更新
        if (_uiRefs.PlayerWPHealthBarImage != null && _playerMaxWP > 0)
        {
            _uiRefs.PlayerWPHealthBarImage.fillAmount = Mathf.SmoothDamp(
                _uiRefs.PlayerWPHealthBarImage.fillAmount,
                (float)_currentPlayerWP / (float)_playerMaxWP,
                ref _playerWPBarVelocity,
                GameConstants.GAUGE_SMOOTH_TIME,
                _maxSpeed,
                Time.unscaledDeltaTime
            );
        }
    }

    /// <summary>
    /// アイテムログの表示時間を管理し、期限切れの項目を削除します。
    /// </summary>
    private void UpdateItemLogUI()
    {
        // 表示対象のアイテムが存在するか確認
        if (_itemLogQueue.Count != 0)
        {
            // キューの先頭にあるアイテムの表示時間が過ぎていれば順に削除
            while (
                _itemLogQueue.Count > 0
                && Time.time - _itemLogQueue.Peek().timestamp > _itemDisplayDuration
            )
            {
                // 先頭のアイテムの表示時間が経過したため、キューから削除
                _itemLogQueue.Dequeue();

                // 表示中のアイテムUIを最新の状態に更新（空いたスロットを反映）
                RefreshItemLogDisplay();
            }
        }
    }

    /// <summary>
    /// ボスのHPバーを滑らかに更新します。
    /// </summary>
    private void UpdateBossUI()
    {
        if (_currentBossGameObject == null)
        {
            return; // ボスがいない場合は処理を終了
        }

        if (_uiRefs.BossHealthBarImage != null && _bossMaxHP > 0)
        {
            _uiRefs.BossHealthBarImage.fillAmount = Mathf.SmoothDamp(
                _uiRefs.BossHealthBarImage.fillAmount,
                (float)_currentBossHP / (float)_bossMaxHP,
                ref _bossHPBarVelocity,
                GameConstants.GAUGE_SMOOTH_TIME,
                _maxSpeed,
                Time.unscaledDeltaTime
            );
        }
    }

    #endregion

    #region Player UI Control

    /// <summary>
    /// プレイヤーの最大HP変更時（または初期化時）に呼び出され、HP関連UIを更新します。
    /// </summary>
    /// <param name="newMaxHP">新しい最大HP</param>
    private void InitializePlayerHPData(int newMaxHP)
    {
        _playerMaxHP = newMaxHP;
        _currentPlayerHP = _playerManager.GetPlayerIntStatus(PlayerStatusIntName.playerCurrentHP);

        if (_uiRefs.PlayerMaxHPText != null)
        {
            _uiRefs.PlayerMaxHPText.text = _playerMaxHP.ToString();
        }

        if (_uiRefs.PlayerHPText != null)
        {
            _uiRefs.PlayerHPText.text = _currentPlayerHP.ToString();
        }

        if (_uiRefs.PlayerHPHealthBarImage != null)
        {
            // 初期化時は即座に反映
            _uiRefs.PlayerHPHealthBarImage.fillAmount =
                (float)_currentPlayerHP / (float)_playerMaxHP;
        }
    }

    /// <summary>
    /// プレイヤーのWPデータを初期化し、UI（テキストとゲージ）に反映します。
    /// </summary>
    private void InitializePlayerWPData()
    {
        _playerMaxWP = _playerManager.playerMaxWP;
        _currentPlayerWP = _playerManager.GetPlayerIntStatus(PlayerStatusIntName.playerCurrentWP);

        if (_uiRefs.PlayerMaxWPText != null)
        {
            _uiRefs.PlayerMaxWPText.text = _playerMaxWP.ToString();
        }

        if (_uiRefs.PlayerWPText != null)
        {
            _uiRefs.PlayerWPText.text = _currentPlayerWP.ToString();
        }

        if (_uiRefs.PlayerWPHealthBarImage != null)
        {
            // 初期化時は即座に反映
            _uiRefs.PlayerWPHealthBarImage.fillAmount =
                (float)_currentPlayerWP / (float)_playerMaxWP;
        }
    }

    /// <summary>
    /// プレイヤーのHP変更イベント（OnChangeHP）から呼び出されるコールバック。
    /// </summary>
    /// <param name="newHP">プレイヤーの新しい現在HP</param>
    private void UpdatePlayerHPCache(int newHP)
    {
        _currentPlayerHP = newHP; // プレイヤーの現在のHPを更新

        // HPテキストを更新
        if (_uiRefs.PlayerHPText != null)
        {
            _uiRefs.PlayerHPText.text = newHP.ToString();
        }
    }

    /// <summary>
    /// プレイヤーのWP変更イベント（OnChangeWP）から呼び出されるコールバック。
    /// </summary>
    /// <param name="newWP">プレイヤーの新しい現在WP</param>
    private void UpdatePlayerWPCache(int newWP)
    {
        _currentPlayerWP = newWP;

        if (_uiRefs.PlayerWPText != null)
        {
            _uiRefs.PlayerWPText.text = _currentPlayerWP.ToString();
        }
    }

    #endregion

    #region Boss UI Control

    /// <summary>
    /// ボス戦開始時に外部から呼び出され、ボスUIの表示とHPイベントの購読を開始します。
    /// </summary>
    /// <param name="bossGameObject">ボスのGameObject</param>
    public void SetGameUIBossData(GameObject bossGameObject)
    {
        _currentBossGameObject = bossGameObject; // ボスゲームオブジェクトを設定
        _currentBossHealthScript = bossGameObject.GetComponent<CharacterHealth>(); // スクリプトへの参照を取得

        if (_currentBossHealthScript == null)
        {
            // スクリプトがない場合でもUIを非表示にするなどの処理は行う
            SetBossUIVisibility(false);
            SetBossBattleState(false); // 念のためfalseに設定
            Debug.LogWarning(
                "ボスオブジェクトにCharacterHealthスクリプトが見つかりません。",
                bossGameObject
            );
            return;
        }

        // 初期データの取得
        _currentBossHP = _currentBossHealthScript.CurrentHP;
        _bossMaxHP = _currentBossHealthScript.MaxHP;
        int bossLevel = _currentBossHealthScript.Level;

        // UI反映
        if (_uiRefs.BossLevelNumberText != null)
        {
            _uiRefs.BossLevelNumberText.text = $"{bossLevel}";
        }
        else
        {
            Debug.LogWarning("ボスのレベルテキストが設定されていません。");
        }

        _currentBossHealthScript.OnHPChanged += OnBossHPChanged; // イベントの購読
        SetBossBattleState(true); // ボス戦闘中フラグをtrueにする

        // ボスのHP関係UIを表示
        // 会話中はUIを非表示(false)にする
        SetBossUIVisibility(!_isTalking);
    }

    /// <summary>
    /// ボスのHPが変更されたときにイベント経由で呼ばれ、内部のボスHP変数を更新します。
    /// </summary>
    /// <param name="bossCurrentHP">ボスの新しい現在HP</param>
    private void OnBossHPChanged(int bossCurrentHP)
    {
        _currentBossHP = bossCurrentHP;
    }

    /// <summary>
    /// ボス戦終了時に外部から呼び出され、ボスUIを非表示にし、HPイベントの購読を解除します。
    /// </summary>
    /// <param name="targetGameObject">ボスのGameObject（対象確認用）</param>
    public void RemoveUIBossData(GameObject targetGameObject)
    {
        if (_currentBossGameObject != targetGameObject)
        {
            return;
        }

        // イベントの購読を解除
        if (_currentBossHealthScript != null)
        {
            _currentBossHealthScript.OnHPChanged -= OnBossHPChanged;
            _currentBossHealthScript = null; // 参照をクリア
        }

        // UIリセット
        SetBossUIVisibility(false);
        if (_uiRefs.BossLevelNumberText != null)
        {
            _uiRefs.BossLevelNumberText.text = "???";
        }

        _currentBossGameObject = null;
        SetBossBattleState(false);
    }

    /// <summary>
    /// 会話状態の変更イベント（GameManager.OnTalkingStateChanged）から呼び出されるコールバック。
    /// </summary>
    /// <param name="talkState">true=会話中, false=会話終了</param>
    private void HandleTalkingStateChanged(bool talkState)
    {
        _isTalking = talkState; // ローカル変数に会話状態を保存

        if (talkState)
        {
            // 会話開始時：実行中のブロックがStoryタイプか確認
            if (IsCurrentBlockStory())
            {
                _isStoryTalking = true;

                if (_uiRefs != null)
                {
                    // 既存のアイテムログ（_uiRefs.ItemLogSlots）をすべて強制的に非表示にする
                    if (_uiRefs.ItemLogSlots != null)
                    {
                        foreach (var slot in _uiRefs.ItemLogSlots)
                        {
                            if (slot != null && slot.gameObject.activeSelf)
                            {
                                slot.gameObject.SetActive(false);
                            }
                        }
                    }

                    // Story会話中はレベルアップや技名表示も非表示にする
                    if (_uiRefs.LevelUpPopup != null && _uiRefs.LevelUpPopup.activeSelf)
                    {
                        _uiRefs.LevelUpPopup.SetActive(false);
                    }

                    if (_uiRefs.SkillNameDisplay != null && _uiRefs.SkillNameDisplay.activeSelf)
                    {
                        _uiRefs.SkillNameDisplay.SetActive(false);
                    }
                }
            }
        }
        else
        {
            // 会話終了時：フラグをリセット
            _isStoryTalking = false;
        }

        if (_currentBossGameObject == null)
        {
            // ボスがいないなら、もちろんボス戦中でもない
            SetBossBattleState(false);
            return;
        }

        // 会話中はボスUIを隠す
        SetBossUIVisibility(!talkState);
    }

    /// <summary>
    /// 現在実行中のFungusブロックが「Story」タイプかどうかを判定します。
    /// </summary>
    private bool IsCurrentBlockStory()
    {
        // 重いFindObjectsOfTypeを廃止し、Fungusが自動管理しているFlowchartリストを参照する
        foreach (Flowchart flowchart in Flowchart.CachedFlowcharts)
        {
            // そのFlowchartが持っているブロックのみを取得（シーン全体検索に比べて圧倒的に軽量）
            Block[] blocksInFlowchart = flowchart.GetComponents<Block>();

            foreach (Block block in blocksInFlowchart)
            {
                // 現在実行中（Executing）のブロックを探し、タイプを判定
                if (block.State == ExecutionState.Executing)
                {
                    return block.TypeOfBlock == BlockType.Story;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// CanvasGroupを使用してボスのUI全体の表示/非表示を制御します。
    /// </summary>
    /// <param name="isVisible">表示する場合はtrue、非表示にする場合はfalse</param>
    private void SetBossUIVisibility(bool isVisible)
    {
        if (_uiRefs.BossHealthUIPanel == null)
        {
            Debug.LogError("BossHealthUIPanelが設定されていません！");
            return;
        }

        if (isVisible)
        {
            _uiRefs.BossHealthUIPanel.alpha = 1f; // 透明度を1にして表示
        }
        else
        {
            _uiRefs.BossHealthUIPanel.alpha = 0f; // 透明度を0にして非表示
        }
    }

    /// <summary>
    /// ボス戦の状態を設定し、必要に応じてイベント（OnBossBattleStateChanged）を発行します。
    /// </summary>
    /// <param name="isFighting">ボス戦中ならtrue</param>
    private void SetBossBattleState(bool isFighting)
    {
        // 既に同じ状態なら、何もしない
        if (this.IsInBossBattle == isFighting)
        {
            return;
        }

        // 状態を更新
        this.IsInBossBattle = isFighting;

        // 状態の変更をイベントで通知
        OnBossBattleStateChanged?.Invoke(isFighting);
    }

    #endregion

    #region Sub UI Control (ItemLog, LevelUp, Skill, FastTravel)

    /// <summary>
    /// 外部から呼び出され、取得したアイテム名をログUIのキューに追加します。
    /// </summary>
    /// <param name="itemName">取得したアイテムの名前</param>
    public void AddGetItemLog(string itemName)
    {
        float now = Time.time;

        // 4つ目が来たら先頭を削除（最大表示数3）
        if (_itemLogQueue.Count >= 3)
            _itemLogQueue.Dequeue();

        // 追加
        _itemLogQueue.Enqueue(new ItemInfo { itemName = itemName, timestamp = now });

        RefreshItemLogDisplay();
    }

    /// <summary>
    /// アイテムログのキュー（recentGetItems）の内容を、実際のUIスロットに反映させます。
    /// </summary>
    private void RefreshItemLogDisplay()
    {
        // ストーリー会話中はアイテムログの表示をキャンセルして終了する
        if (_isStoryTalking)
        {
            return;
        }

        // Queueを配列に変換して、インデックスアクセスを可能にする
        var itemsArray = _itemLogQueue.ToArray();

        for (int i = 0; i < _uiRefs.ItemLogSlots.Count; i++)
        {
            // アイテムログのスロットを取得
            var itemText = _uiRefs.ItemLogSlots[i].GetComponentInChildren<TextMeshProUGUI>();

            if (i < itemsArray.Length)
            {
                itemText.text = itemsArray[i].itemName; // アイテム名を設定
                _uiRefs.ItemLogSlots[i].SetActive(true); // スロットを表示
            }
            else
            {
                itemText.text = ""; // アイテム名を空にする
                _uiRefs.ItemLogSlots[i].SetActive(false); // スロットを非表示
            }
        }
    }

    /// <summary>
    /// 外部から呼び出され、レベルアップのポップアップUIを表示します。
    /// </summary>
    /// <param name="level">新しいレベル</param>
    public void ShowLevelUpUI(int level)
    {
        // ストーリー会話中は新規表示をキャンセルする
        if (_isStoryTalking)
            return;

        if (_uiRefs.LevelUpPopup == null)
            return;

        _uiRefs.LevelUpPopup.SetActive(true);

        // UI内のテキストを更新
        TextMeshProUGUI text = _uiRefs.LevelUpPopup.GetComponentInChildren<TextMeshProUGUI>();
        if (text != null)
        {
            text.text = $"レベル {level} にアップ！";
        }

        StartCoroutine(HideLevelUpUIAfterDelay());
    }

    /// <summary>
    /// （コルーチン）レベルアップUIを一定時間表示した後に非表示にします。
    /// </summary>
    private IEnumerator HideLevelUpUIAfterDelay()
    {
        yield return new WaitForSeconds(_levelUpDisplayTime);
        _uiRefs.LevelUpPopup.SetActive(false);
    }

    /// <summary>
    /// 外部から呼び出され、技名のUIを表示します。
    /// </summary>
    /// <param name="skillName">表示する技名</param>
    public void ShowSkillNameUI(string skillName)
    {
        // ストーリー会話中は新規表示をキャンセルする
        if (_isStoryTalking)
            return;

        if (_uiRefs.SkillNameDisplay == null || _uiRefs.SkillNameText == null)
            return;

        _uiRefs.SkillNameText.text = skillName;
        _uiRefs.SkillNameDisplay.SetActive(true);

        StartCoroutine(HideSkillNameUIAfterDelay());
    }

    /// <summary>
    /// （コルーチン）技名UIを一定時間表示した後に非表示にします。
    /// </summary>
    private IEnumerator HideSkillNameUIAfterDelay()
    {
        yield return new WaitForSeconds(_skillNameDisplayTime);
        _uiRefs.SkillNameDisplay.SetActive(false);
    }

    /// <summary>
    /// 外部から呼び出され、ファストトラベルのパネルUIを開きます。
    /// </summary>
    public void OpenFastTravelPanel()
    {
        if (_uiRefs.FastTravelPanel == null)
        {
            Debug.LogError("ファストトラベルのパネルUIが設定されていません");
            return;
        }

        var fastTravelPanelActive = _uiRefs.FastTravelPanel.GetComponent<FastTravelPanelActive>();
        if (fastTravelPanelActive != null)
        {
            fastTravelPanelActive.OpenFastTravelPanel();
        }
    }

    #endregion
}
