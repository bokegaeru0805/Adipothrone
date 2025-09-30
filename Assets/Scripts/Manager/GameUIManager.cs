using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class GameUIManager : MonoBehaviour
{
    public static GameUIManager instance { get; private set; }
    private PlayerManager playerManager;

    [Header("UI参照のルート")]
    [SerializeField]
    private GameUIRefs uiRefs = null;
    private Queue<ItemInfo> recentGetItems = new Queue<ItemInfo>();
    private float itemDisplayDuration = 5f; // アイテム獲得UI表示時間（秒）
    private float levelUpDisplayTime = 3f; // レベルアップポップアップの表示時間（秒）
    private float skillNameDisplayTime = 3f; // 技名表示の表示時間（秒）

    private class ItemInfo
    {
        public string itemName;
        public float timestamp;
    }

    private int playerHP;
    private int playerMaxHP;
    private int playerWP;
    private int playerMaxWP;
    private int bossHP;
    private int bossMaxHP;
    private float _maxSpeed = float.PositiveInfinity; // 最高速度
    private float _playerCurrentHPVelocity = 0f;
    private float _playerCurrentWPVelocity = 0f;
    private float _bossCurrentVelocity = 0f;
    private GameObject bossObject = null;
    private CharacterHealth currentBossHPScript = null; // 現在のボスHPスクリプト

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            if (uiRefs == null)
            {
                Debug.LogError("GameUIManagerにGameUIRefsが設定されていません！");
                return;
            }

            if (
                uiRefs.BossHealthBarImage == null
                || uiRefs.BossHealthUIPanel == null
                || uiRefs.BossLevelNumberText == null
            )
            {
                Debug.LogError(
                    "GameUIRefsにボスのHPバー、背景、、レベルUI、レベル番号テキストが設定されていません"
                );
                return;
            }
            else
            {
                SetBossUIVisibility(false);; //ボスのHPバーのパネルを非表示
                uiRefs.BossLevelNumberText.text = $"???"; //ボスのレベルテキストをリセット
            }

            if (uiRefs.SkillNameDisplay == null)
            {
                Debug.LogError("GameUIRefsに技名表示のUIが設定されていません");
                return;
            }
            else
            {
                uiRefs.SkillNameDisplay.SetActive(false); // 技名表示のUIを非表示にする
            }

            if (uiRefs.FastTravelPanel == null)
            {
                Debug.LogError("GameUIRefsにファストトラベルのパネルUIが設定されていません");
            }
            else
            {
                uiRefs.FastTravelPanel.SetActive(false); // ファストトラベルのパネルUIを非表示にする
            }

            // 入手アイテムのログのUIを非表示にする
            foreach (var slot in uiRefs.ItemLogSlots)
            {
                slot.SetActive(false);
            }

            // レベルアップのポップアップのUIを非表示にする
            if (uiRefs.LevelUpPopup != null)
            {
                uiRefs.LevelUpPopup.SetActive(false);
            }
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // StartでPlayerManagerのインスタンスを一度だけ取得し、保持する
        playerManager = PlayerManager.instance;
        if (playerManager == null)
        {
            Debug.LogError(
                "PlayerManagerのインスタンスが見つかりません！このスクリプトは動作しません。"
            );
            return; // PlayerManagerがなければ、ここで処理を中断
        }

        if (playerManager != null)
        {
            playerManager.OnChangeHP += GetPlayerHPData;
            playerManager.OnChangeWP += OnChangeWP;
            playerManager.OnChangeMaxHP += InitializePlayerHPData;
            InitializePlayerHPData(playerManager.playerMaxHP); //プレイヤーのHPの初期値を取得
            OnChangeWP(playerManager.GetPlayerIntStatus(PlayerStatusIntName.playerCurrentWP)); // プレイヤーのWPの初期値を取得
        }
        else
        {
            Debug.LogError("PlayerManagerが見つかりません。GameUIManagerは動作しません。");
        }

        GameManager.OnTalkingStateChanged += SetActiveBossUI; // 会話状態の変更に応じてボスのUIをアクティブにする

        InitializePlayerWPData(); // プレイヤーのWPの初期値を取得
    }

    private void OnDisable()
    {
        // オブジェクトが破棄される際などにも呼ばれるため、playerManagerが存在するか確認
        if (playerManager != null)
        {
            playerManager.OnChangeHP -= GetPlayerHPData;
            playerManager.OnChangeWP -= OnChangeWP;
            playerManager.OnChangeMaxHP -= InitializePlayerHPData;
        }

        GameManager.OnTalkingStateChanged -= SetActiveBossUI; // 会話状態の変更に応じてボスのUIをアクティブにするイベントの購読を解除
    }

    private void Update()
    {
        //プレイヤーのHPに関するUIの更新
        if (uiRefs.PlayerHPHealthBarImage != null && playerMaxHP > 0)
        {
            uiRefs.PlayerHPHealthBarImage.fillAmount = Mathf.SmoothDamp(
                uiRefs.PlayerHPHealthBarImage.fillAmount,
                (float)playerHP / (float)playerMaxHP,
                ref _playerCurrentHPVelocity,
                GameConstants.GaugeSmoothTime,
                _maxSpeed,
                Time.unscaledDeltaTime
            );
        }

        //プレイヤーのWPに関するUIの更新
        if (uiRefs.PlayerWPHealthBarImage != null && playerMaxWP > 0)
        {
            uiRefs.PlayerWPHealthBarImage.fillAmount = Mathf.SmoothDamp(
                uiRefs.PlayerWPHealthBarImage.fillAmount,
                (float)playerWP / (float)playerMaxWP,
                ref _playerCurrentWPVelocity,
                GameConstants.GaugeSmoothTime,
                _maxSpeed,
                Time.unscaledDeltaTime
            );
        }

        // 表示対象のアイテムが存在するか確認
        if (recentGetItems.Count != 0)
        {
            // キューの先頭にあるアイテムの表示時間が過ぎていれば順に削除
            while (
                recentGetItems.Count > 0
                && Time.time - recentGetItems.Peek().timestamp > itemDisplayDuration
            )
            {
                // 先頭のアイテムの表示時間が経過したため、キューから削除
                recentGetItems.Dequeue();

                // 表示中のアイテムUIを最新の状態に更新（空いたスロットを反映）
                UpdateItemUI();
            }
        }

        if (bossObject == null)
        { //ボスがいない場合は処理を終了
            return;
        }

        if (uiRefs.BossHealthBarImage != null && bossObject != null && bossMaxHP > 0)
        {
            uiRefs.BossHealthBarImage.fillAmount = Mathf.SmoothDamp(
                uiRefs.BossHealthBarImage.fillAmount,
                (float)bossHP / (float)bossMaxHP,
                ref _bossCurrentVelocity,
                GameConstants.GaugeSmoothTime,
                _maxSpeed,
                Time.unscaledDeltaTime
            );
        }
    }

    // プレイヤーのHPの初期値を取得
    private void InitializePlayerHPData(int newMaxHP)
    {
        playerMaxHP = newMaxHP;
        playerHP = playerManager.GetPlayerIntStatus(PlayerStatusIntName.playerCurrentHP);

        if (uiRefs.PlayerMaxHPText != null)
        {
            uiRefs.PlayerMaxHPText.text = playerMaxHP.ToString();
        }

        if (uiRefs.PlayerHPText != null)
        {
            uiRefs.PlayerHPText.text = playerHP.ToString();
        }

        if (uiRefs.PlayerHPHealthBarImage != null)
        {
            uiRefs.PlayerHPHealthBarImage.fillAmount = (float)playerHP / (float)playerMaxHP;
        }
    }

    // プレイヤーのWPの初期値を取得するメソッド
    private void InitializePlayerWPData()
    {
        playerMaxWP = playerManager.playerMaxWP;
        playerWP = playerManager.GetPlayerIntStatus(PlayerStatusIntName.playerCurrentWP);

        if (uiRefs.PlayerMaxWPText != null)
        {
            uiRefs.PlayerMaxWPText.text = playerMaxWP.ToString();
        }

        if (uiRefs.PlayerWPText != null)
        {
            uiRefs.PlayerWPText.text = playerWP.ToString();
        }

        if (uiRefs.PlayerWPHealthBarImage != null)
        {
            uiRefs.PlayerWPHealthBarImage.fillAmount = (float)playerWP / (float)playerMaxWP;
        }
    }

    //ボスのHPを取得
    private void GetBossData(int bossCurrentHP)
    {
        bossHP = bossCurrentHP;
    }

    //プレイヤーのHPデータを取得するメソッド
    private void GetPlayerHPData()
    {
        playerHP = playerManager.GetPlayerIntStatus(PlayerStatusIntName.playerCurrentHP);

        if (uiRefs.PlayerHPText != null)
        {
            uiRefs.PlayerHPText.text = playerHP.ToString();
        }
    }

    //プレイヤーのWPデータを取得するメソッド
    private void OnChangeWP(int newWP)
    {
        playerWP = newWP;

        if (uiRefs.PlayerWPText != null)
        {
            uiRefs.PlayerWPText.text = playerWP.ToString();
        }
    }

    //ボスのUIデータを設定するメソッド
    public void SetGameUIBossData(GameObject gameObject)
    {
        bossObject = gameObject; //ボスゲームオブジェクトを設定
        currentBossHPScript = gameObject.GetComponent<CharacterHealth>(); //スクリプトへの参照を取得

        if (currentBossHPScript == null)
        {
            // スクリプトがない場合でもUIを非表示にするなどの処理は行う
            SetBossUIVisibility(false);;
            Debug.LogWarning("ボスオブジェクトにboss_HPスクリプトが見つかりません。", gameObject);
            return;
        }

        bossHP = currentBossHPScript.CurrentHP; //ボスの現在のHPを取得
        bossMaxHP = currentBossHPScript.MaxHP; //ボスの最大HPを取得
        int bossLevel = currentBossHPScript.Level; //ボスのレベルを取得
        if (uiRefs.BossLevelNumberText != null)
        {
            uiRefs.BossLevelNumberText.text = $"{bossLevel}"; //ボスのレベルをUIに設定
        }
        else
        {
            Debug.LogWarning("ボスのレベルテキストが設定されていません。");
        }

        currentBossHPScript.OnHPChanged += GetBossData; //イベントの購読

        // ボスのHP関係UIを表示
        //GameManager.IsTalkingがtrue、つまり会話中はUIを非表示にする
        SetBossUIVisibility(!GameManager.IsTalking);
    }

    //ボスのUIデータを削除するメソッド
    public void RemoveUIBossData(GameObject gameObject)
    {
        if (bossObject != gameObject)
        {
            return;
        }

        // イベントの購読を解除
        if (currentBossHPScript != null)
        {
            currentBossHPScript.OnHPChanged -= GetBossData;
            currentBossHPScript = null; // 参照をクリア
        }

        SetBossUIVisibility(false); //ボスのHPバーのパネルを非表示
        if (uiRefs.BossLevelNumberText != null)
        {
            uiRefs.BossLevelNumberText.text = $"???"; //ボスのレベルテキストをリセット
        }
        bossObject = null; //ボスゲームオブジェクトをnullにする
    }

    // ボスのUIをアクティブにするメソッド
    private void SetActiveBossUI(bool isActive)
    {
        if (bossObject == null)
        {
            return; // ボスオブジェクトが存在しない場合は何もしない
        }

        SetBossUIVisibility(!isActive);
    }

    /// <summary>
    /// CanvasGroupを使用してボスのUI全体の表示/非表示を制御します。
    /// </summary>
    /// <param name="isVisible">表示する場合はtrue、非表示にする場合はfalse</param>
    private void SetBossUIVisibility(bool isVisible)
    {
        if (uiRefs.BossHealthUIPanel == null)
        {
            Debug.LogError("BossHealthUIPanelが設定されていません！");
            return;
        }

        if (isVisible)
        {
            uiRefs.BossHealthUIPanel.alpha = 1f; // 透明度を1にして表示
            // uiRefs.BossHealthUIPanel.interactable = true; // 操作を有効化
            // uiRefs.BossHealthUIPanel.blocksRaycasts = true; // マウスイベントなどをブロック
        }
        else
        {
            uiRefs.BossHealthUIPanel.alpha = 0f; // 透明度を0にして非表示
            // uiRefs.BossHealthUIPanel.interactable = false; // 操作を無効化
            // uiRefs.BossHealthUIPanel.blocksRaycasts = false; // マウスイベントなどを透過
        }
    }

    // アイテムを取得したときにログを追加するメソッド
    public void AddGetItemLog(string itemName)
    {
        float now = Time.time;

        // 4つ目が来たら先頭を削除
        if (recentGetItems.Count >= 3)
            recentGetItems.Dequeue();

        // 追加
        recentGetItems.Enqueue(new ItemInfo { itemName = itemName, timestamp = now });

        UpdateItemUI();
    }

    // アイテムログのUIを更新するメソッド
    private void UpdateItemUI()
    {
        // recentGetItems（Queue型）を配列に変換して、インデックスアクセスを可能にする。
        // Queueはインデックスアクセス（items[0]など）ができないため、UI表示で順番に参照するために配列に変換している。
        var itemsArray = recentGetItems.ToArray();

        for (int i = 0; i < uiRefs.ItemLogSlots.Count; i++)
        {
            // アイテムログのスロットを取得
            TextMeshProUGUI itemText = uiRefs
                .ItemLogSlots[i]
                .GetComponentInChildren<TextMeshProUGUI>();

            if (i < itemsArray.Length)
            {
                itemText.text = itemsArray[i].itemName; // アイテム名を設定
                uiRefs.ItemLogSlots[i].SetActive(true); // アイテムログのスロットをアクティブにする
            }
            else
            {
                itemText.text = ""; // アイテム名を空にする
                uiRefs.ItemLogSlots[i].SetActive(false); // アイテムログのスロットを非アクティブにする
            }
        }
    }

    // レベルアップのポップアップを表示するメソッド
    public void ShowLevelUpUI(int level)
    {
        if (uiRefs.LevelUpPopup == null)
            return;

        uiRefs.LevelUpPopup.SetActive(true);

        // UI内のテキストを更新（TextMeshProUGUI を使っている前提）
        TextMeshProUGUI text = uiRefs.LevelUpPopup.GetComponentInChildren<TextMeshProUGUI>();
        if (text != null)
        {
            text.text = $"レベル {level} にアップ！";
        }

        StartCoroutine(HideLevelUpUIAfterDelay());
    }

    private IEnumerator HideLevelUpUIAfterDelay()
    {
        yield return new WaitForSeconds(levelUpDisplayTime);
        uiRefs.LevelUpPopup.SetActive(false);
    }

    // 技名表示のUIを表示するメソッド
    public void ShowSkillNameUI(string skillName)
    {
        if (uiRefs.SkillNameDisplay == null || uiRefs.SkillNameText == null)
            return;

        uiRefs.SkillNameText.text = skillName;
        uiRefs.SkillNameDisplay.SetActive(true);

        StartCoroutine(HideSkillNameUIAfterDelay());
    }

    private IEnumerator HideSkillNameUIAfterDelay()
    {
        yield return new WaitForSeconds(skillNameDisplayTime);
        uiRefs.SkillNameDisplay.SetActive(false);
    }

    // ファストトラベルのパネルを開くメソッド
    public void OpenFastTravelPanel()
    {
        if (uiRefs.FastTravelPanel == null)
        {
            Debug.LogError("ファストトラベルのパネルUIが設定されていません");
            return;
        }

        var fastTravelPanelActive = uiRefs.FastTravelPanel.GetComponent<FastTravelPanelActive>();
        if (fastTravelPanelActive != null)
        {
            fastTravelPanelActive.OpenFastTravelPanel();
        }
    }
}
