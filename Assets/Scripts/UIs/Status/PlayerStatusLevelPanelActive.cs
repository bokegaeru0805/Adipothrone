using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// ステータス画面のUIを制御するクラス。
/// 経験値レベル（天井）、アイテム解放レベル（最大）、現在レベル（縛り）の3つを視覚化し、
/// リアルタイムでステータス実数値を更新します。
/// </summary>
public class PlayerStatusLevelPanelActive : MonoBehaviour, IPanelActive
{
    [Header("UI References - Left Panel")]
    [SerializeField]
    private TextMeshProUGUI wpText; // 例: "WP: 15 / 15"

    [SerializeField]
    private TextMeshProUGUI expLevelText; // 例: "現在のレベル: 12"

    [Header("UI References - Right Panel (Sliders)")]
    [SerializeField]
    private Slider hpSlider;

    [SerializeField]
    private Slider attackSlider;

    [SerializeField]
    private Slider defenseSlider;

    [SerializeField]
    private Slider speedSlider;

    [SerializeField]
    private Slider luckSlider;

    [Header("UI References - Right Panel (Level Texts)")]
    // 例: "Lv 5 / 8" と表示するためのテキスト
    [SerializeField]
    private TextMeshProUGUI hpLevelText;

    [SerializeField]
    private TextMeshProUGUI attackLevelText;

    [SerializeField]
    private TextMeshProUGUI defenseLevelText;

    [SerializeField]
    private TextMeshProUGUI speedLevelText;

    [SerializeField]
    private TextMeshProUGUI luckLevelText;

    [Header("UI References - Right Panel (Stat Texts)")]
    // 例: "基礎攻撃力: 125" と表示するためのテキスト
    [SerializeField]
    private TextMeshProUGUI hpStatText;

    [SerializeField]
    private TextMeshProUGUI attackStatText;

    [SerializeField]
    private TextMeshProUGUI defenseStatText;

    [SerializeField]
    private TextMeshProUGUI speedStatText;

    [SerializeField]
    private TextMeshProUGUI luckStatText;

    [Header("Focus Management")]
    [SerializeField]
    private GameObject[] myButtons; // 4つのスライダーのGameObjectをインスペクターでアタッチする

    private int lastSelectedIndex = -1; // 最後に選択していたボタンの位置を記憶
    private bool isUpdatingUI = false; // スクリプトからスライダーの値を変更した際の無限ループ防止フラグ

    private void Awake()
    {
        // スライダーのイベントリスナーを登録
        if (hpSlider != null)
            hpSlider.onValueChanged.AddListener(val =>
                OnSliderValueChanged(
                    hpSlider,
                    PlayerStatusIntName.hpMaxLevel,
                    PlayerStatusIntName.hpCurrentLevel,
                    val
                )
            );
        if (attackSlider != null)
            attackSlider.onValueChanged.AddListener(val =>
                OnSliderValueChanged(
                    attackSlider,
                    PlayerStatusIntName.attackMaxLevel,
                    PlayerStatusIntName.attackCurrentLevel,
                    val
                )
            );
        if (defenseSlider != null)
            defenseSlider.onValueChanged.AddListener(val =>
                OnSliderValueChanged(
                    defenseSlider,
                    PlayerStatusIntName.defenceMaxLevel,
                    PlayerStatusIntName.defenceCurrentLevel,
                    val
                )
            );
        if (speedSlider != null)
            speedSlider.onValueChanged.AddListener(val =>
                OnSliderValueChanged(
                    speedSlider,
                    PlayerStatusIntName.speedMaxLevel,
                    PlayerStatusIntName.speedCurrentLevel,
                    val
                )
            );
        if (luckSlider != null)
            luckSlider.onValueChanged.AddListener(val =>
                OnSliderValueChanged(
                    luckSlider,
                    PlayerStatusIntName.luckMaxLevel,
                    PlayerStatusIntName.luckCurrentLevel,
                    val
                )
            );
    }

    private void OnEnable()
    {
        // パネルが開かれたらUIの最新状態を取得して表示し、フォーカスを当てる
        RefreshAllUI();
        SelectFirstButton();
    }

    private void OnDisable()
    {
        // タブが切り替わる直前に、現在のカーソル位置を記憶する
        GameObject currentSelected = EventSystem.current.currentSelectedGameObject;
        if (currentSelected != null && myButtons != null)
        {
            lastSelectedIndex = System.Array.IndexOf(myButtons, currentSelected);
        }
    }

    #region フォーカス制御 (NewTabSubPanelTemplateの機能)

    public void SelectFirstButton()
    {
        GameObject targetButton = null;
        if (lastSelectedIndex >= 0 && lastSelectedIndex < myButtons.Length)
        {
            targetButton = myButtons[lastSelectedIndex]; // 前回位置を復元
        }
        else if (myButtons.Length > 0)
        {
            targetButton = myButtons[0]; // なければ先頭
        }

        if (targetButton != null)
        {
            StartCoroutine(SelectButtonAfterDelay(targetButton));
        }
        else
        {
            EventSystem.current.SetSelectedGameObject(null);
        }

        Debug.Log(
            $"PlayerStatusLevelPanelActive: SelectFirstButton called. Restoring index {lastSelectedIndex}, target: {targetButton?.name}"
        );
    }

    private IEnumerator SelectButtonAfterDelay(GameObject targetObj)
    {
        yield return new WaitForEndOfFrame();
        yield return null;

        if (targetObj != null && targetObj.activeInHierarchy)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(targetObj);
        }
    }

    #endregion

    #region UI更新とスライダー制御

    /// <summary>
    /// 全てのUI表示（左側の基本情報と右側のスライダー・数値）を最新の状態に更新します。
    /// </summary>
    private void RefreshAllUI()
    {
        if (PlayerManager.instance == null || PlayerLevelManager.instance == null)
            return;

        isUpdatingUI = true; // プログラムからスライダーを動かすためフラグを立てる

        // 左側パネルの更新（HP, WP, 経験値レベル）
        int maxWP = PlayerManager.instance.playerMaxWP;
        int expLevel = PlayerLevelManager.instance.playerLv;

        if (wpText != null)
            wpText.text = $"最大WP 　: {maxWP}";
        if (expLevelText != null)
            expLevelText.text = $"レベル: {expLevel}";

        // 右側パネルの更新（スライダーの最大値＝天井、表示値＝現在レベル）
        UpdateSliderState(
            hpSlider,
            hpLevelText,
            hpStatText,
            PlayerStatusIntName.hpMaxLevel,
            PlayerStatusIntName.hpCurrentLevel,
            expLevel
        );
        UpdateSliderState(
            attackSlider,
            attackLevelText,
            attackStatText,
            PlayerStatusIntName.attackMaxLevel,
            PlayerStatusIntName.attackCurrentLevel,
            expLevel
        );
        UpdateSliderState(
            defenseSlider,
            defenseLevelText,
            defenseStatText,
            PlayerStatusIntName.defenceMaxLevel,
            PlayerStatusIntName.defenceCurrentLevel,
            expLevel
        );
        UpdateSliderState(
            speedSlider,
            speedLevelText,
            speedStatText,
            PlayerStatusIntName.speedMaxLevel,
            PlayerStatusIntName.speedCurrentLevel,
            expLevel
        );
        UpdateSliderState(
            luckSlider,
            luckLevelText,
            luckStatText,
            PlayerStatusIntName.luckMaxLevel,
            PlayerStatusIntName.luckCurrentLevel,
            expLevel
        );

        isUpdatingUI = false;
    }

    /// <summary>
    /// 個別のスライダーと対応するテキスト表示を更新します。
    /// </summary>
    private void UpdateSliderState(
        Slider slider,
        TextMeshProUGUI levelText,
        TextMeshProUGUI statText,
        PlayerStatusIntName maxEnum,
        PlayerStatusIntName currentEnum,
        int expLevel
    )
    {
        if (slider == null)
            return;

        int maxLv = PlayerManager.instance.GetPlayerIntStatus(maxEnum);
        int currentLv = PlayerManager.instance.GetPlayerIntStatus(currentEnum);

        // スライダーの全体幅（天井）は現在の経験値レベル
        // ※ただし、最低でも解放済みのmaxLvは表示できるようにする（経験値レベルが低い場合の安全策）
        slider.maxValue = Mathf.Max(expLevel, maxLv);
        slider.minValue = 1;
        slider.value = currentLv;

        if (levelText != null)
        {
            levelText.text = $"Lv {currentLv} / {maxLv}";
        }

        // 実際のステータス数値を PlayerStatusLevelManager から取得して表示
        if (statText != null && PlayerManager.instance.StatusLevelManager != null)
        {
            switch (maxEnum)
            {
                case PlayerStatusIntName.attackMaxLevel:
                    statText.text =
                        $"基礎攻撃力: {PlayerManager.instance.StatusLevelManager.TotalBaseAttackPower}";
                    break;
                case PlayerStatusIntName.defenceMaxLevel:
                    statText.text =
                        $"防御力　　: {PlayerManager.instance.StatusLevelManager.TotalBaseDefensePower}";
                    break;
                case PlayerStatusIntName.speedMaxLevel:
                    // 100を掛けてパーセント表記にする（例: 0.1 → +10%）
                    statText.text =
                        $"素早さ補正: +{PlayerManager.instance.StatusLevelManager.SpeedBonus * 100:F0}%";
                    break;
                case PlayerStatusIntName.luckMaxLevel:
                    statText.text =
                        $"幸運補正　: +{PlayerManager.instance.StatusLevelManager.LuckBonus * 100:F0}%";
                    break;
            }
        }
    }

    /// <summary>
    /// プレイヤーがUIスライダーを操作した際に呼ばれるイベント。
    /// </summary>
    private void OnSliderValueChanged(
        Slider slider,
        PlayerStatusIntName maxEnum,
        PlayerStatusIntName currentEnum,
        float value
    )
    {
        if (isUpdatingUI)
            return; // プログラムによる値変更時は無視する

        int maxLevel = PlayerManager.instance.GetPlayerIntStatus(maxEnum);
        int targetValue = Mathf.RoundToInt(value);

        // クランプ処理（解放済みの最大レベルまでしかドラッグできないようにする）
        if (targetValue > maxLevel)
            targetValue = maxLevel;
        if (targetValue < 1)
            targetValue = 1;

        int currentLevel = PlayerManager.instance.GetPlayerIntStatus(currentEnum);

        // 値が実際に変動した場合のみ、Managerの更新とUIの再描画を行う
        if (currentLevel != targetValue)
        {
            PlayerManager.instance.StatusLevelManager.SetCurrentStatusLevel(
                maxEnum,
                currentEnum,
                targetValue
            );
            RefreshAllUI(); // 数値テキストなどをリアルタイムで更新
        }

        // スライダーが小数点位置に止まった場合や、上限を突破しようとした場合の視覚的な押し戻し
        if (slider.value != targetValue)
        {
            isUpdatingUI = true;
            slider.value = targetValue;
            isUpdatingUI = false;
        }
    }

    #endregion
}
