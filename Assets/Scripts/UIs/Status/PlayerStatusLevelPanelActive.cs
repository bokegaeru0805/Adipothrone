using System.Collections;
using TMPro;
using UnityEditor.EditorTools;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// プレイヤーのステータス画面UIを総括して制御するクラス。
///
/// 【主な役割】
/// 1. プレイヤーの現在の経験値レベル（スライダーの最大幅・天井）を表示。
/// 2. アイテムやイベントで解放したステータスの「最大レベル（解放済みレベル）」を表示。
/// 3. プレイヤー自身が縛りプレイなどのために任意に下げることのできる「現在レベル」をスライダーで操作・視覚化。
/// 4. 変更されたレベルに基づき、実際の基礎ステータス値（攻撃力や素早さ補正など）をリアルタイムに計算・反映します。
/// </summary>
public class PlayerStatusLevelPanelActive : MonoBehaviour, IPanelActive
{
    #region UIコンポーネント参照 (左パネル・共通情報)

    [Header("UI References - Left Panel (Common Info)")]
    [Tooltip("プレイヤーの現在の最大WP（ウェポンポイント）を表示するテキスト")]
    [SerializeField]
    private TextMeshProUGUI wpText; // 例: "最大WP: 15"

    [Tooltip("プレイヤーの現在の経験値レベル（全てのステータスの上限値）を表示するテキスト")]
    [SerializeField]
    private TextMeshProUGUI expLevelText; // 例: "レベル: 12"

    [Tooltip("プレイヤーの現在のステータスレベルを表示するテキスト")]
    [SerializeField]
    private TextMeshProUGUI statusLevelText; // 例: "総合レベル: 10"
    #endregion

    #region UIコンポーネント参照 (右パネル・ステータス詳細)

    [Header("UI References - Right Panel (Sliders)")]
    [Tooltip("現在レベルを操作・視覚化するためのスライダー群")]
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
    [Tooltip("各ステータスの「現在レベル / 最大(解放済み)レベル」を表示するテキスト群")]
    [SerializeField]
    private TextMeshProUGUI hpLevelText; // 例: "Lv 5 / 8"

    [SerializeField]
    private TextMeshProUGUI attackLevelText;

    [SerializeField]
    private TextMeshProUGUI defenseLevelText;

    [SerializeField]
    private TextMeshProUGUI speedLevelText;

    [SerializeField]
    private TextMeshProUGUI luckLevelText;

    [Header("UI References - Right Panel (Stat Texts)")]
    [Tooltip("現在のレベル設定に基づいて算出された、実際の基礎ステータス数値を表示するテキスト群")]
    [SerializeField]
    private TextMeshProUGUI hpStatText; // 例: "最大HP: 250"

    [SerializeField]
    private TextMeshProUGUI attackStatText; // 例: "基礎攻撃力: 125"

    [SerializeField]
    private TextMeshProUGUI defenseStatText;

    [SerializeField]
    private TextMeshProUGUI speedStatText;

    [SerializeField]
    private TextMeshProUGUI luckStatText;

    #endregion

    #region 内部状態変数・フォーカス管理

    [Header("Focus Management")]
    [Tooltip("コントローラー操作用に、フォーカスを当てるスライダー(UI)のリストを順番に設定します")]
    [SerializeField]
    private GameObject[] myButtons;

    /// <summary>最後に選択していたUIオブジェクトの配列インデックス。画面復帰時の位置記憶に使用します。</summary>
    private int lastSelectedIndex = -1;

    /// <summary>スクリプトからスライダーの値を変更した際、OnValueChangedイベントが無限ループするのを防ぐための安全フラグ。</summary>
    private bool isUpdatingUI = false;

    #endregion

    #region Unity ライフサイクルメソッド

    /// <summary>
    /// オブジェクト生成時の初期化処理。
    /// 各スライダーの値が変更された際のイベント（リスナー）を登録します。
    /// </summary>
    private void Awake()
    {
        // ユーザーがスライダーを動かした際、どのステータスを操作したかを判別できるよう、
        // スライダー本体と、対応する「最大レベルのEnum」「現在レベルのEnum」をセットでメソッドに渡します。

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
                    PlayerStatusIntName.defenseMaxLevel,
                    PlayerStatusIntName.defenseCurrentLevel,
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

    /// <summary>
    /// このパネル（画面）が表示された際に呼ばれる処理。
    /// 最新のプレイヤー情報を取得してUIを描画し、適切なボタンにフォーカスを当てます。
    /// </summary>
    private void OnEnable()
    {
        RefreshAllUI();
        SelectFirstButton();
    }

    /// <summary>
    /// このパネルが非表示になる際（タブ切り替え等）に呼ばれる処理。
    /// 次回画面を開いた時のために、現在選択されているカーソル位置を記憶します。
    /// </summary>
    private void OnDisable()
    {
        GameObject currentSelected = EventSystem.current.currentSelectedGameObject;
        if (currentSelected != null && myButtons != null)
        {
            lastSelectedIndex = System.Array.IndexOf(myButtons, currentSelected);
        }
    }

    #endregion

    #region IPanelActive インターフェース実装 (フォーカス制御)

    /// <summary>
    /// 画面展開時などに、初期フォーカスを設定するメソッド。
    /// 記憶していた前回位置があればそこへ、なければ先頭の項目へフォーカスを当てます。
    /// </summary>
    public void SelectFirstButton()
    {
        GameObject targetButton = null;

        // 前回選択していた位置が有効な範囲内であれば復元
        if (lastSelectedIndex >= 0 && lastSelectedIndex < myButtons.Length)
        {
            targetButton = myButtons[lastSelectedIndex];
        }
        // なければ先頭の要素を選択
        else if (myButtons.Length > 0)
        {
            targetButton = myButtons[0];
        }

        if (targetButton != null)
        {
            StartCoroutine(SelectButtonAfterDelay(targetButton));
        }
        else
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    /// <summary>
    /// EventSystemのクリック判定残りなどの競合を防ぐため、
    /// 1フレーム待機してから確実にフォーカスをセットするコルーチン。
    /// </summary>
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

    #region UI表示の初期化・更新処理

    /// <summary>
    /// プレイヤーの情報を取得し、全てのUI表示（左側の基本情報と右側のスライダー・数値）を最新の状態に更新します。
    /// 画面を開いた時や、スライダーを操作した直後に呼ばれます。
    /// </summary>
    private void RefreshAllUI()
    {
        if (PlayerManager.instance == null || PlayerLevelManager.instance == null)
            return;

        // スクリプトからスライダーの値を直接書き換えるため、
        // OnValueChangedイベントが走らないように安全フラグを立てる
        isUpdatingUI = true;

        // 1. 左側パネルの更新（WP, 経験値レベル,ステータスレベル）
        int maxWP = PlayerManager.instance.playerMaxWP;
        int expLevel = PlayerLevelManager.instance.playerLv;
        int statusLevel = PlayerManager.instance.StatusLevelManager.TotalStatusLevel;

        if (wpText != null)
            wpText.text = $"最大WP 　: {maxWP}";
        if (expLevelText != null)
            expLevelText.text = $"レベル: {expLevel}";
        if (statusLevelText != null)
            statusLevelText.text = $"総合ランク: {statusLevel}";

        // 2. 右側パネルのスライダーとテキストの更新
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
            PlayerStatusIntName.defenseMaxLevel,
            PlayerStatusIntName.defenseCurrentLevel,
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

        // 処理が終わったのでフラグを下げる
        isUpdatingUI = false;
    }

    /// <summary>
    /// 指定された1つのステータスについて、スライダーの幅や値、テキストの表記を更新します。
    /// </summary>
    /// <param name="slider">更新対象のスライダーコンポーネント</param>
    /// <param name="levelText">「Lv 現在 / 最大」を表示するテキストコンポーネント</param>
    /// <param name="statText">「基礎攻撃力: 100」のような実数値を表示するテキストコンポーネント</param>
    /// <param name="maxEnum">対象ステータスの「解放済み最大レベル」を示すキー</param>
    /// <param name="currentEnum">対象ステータスの「現在設定しているレベル」を示すキー</param>
    /// <param name="expLevel">プレイヤーの現在の経験値レベル（スライダーの最大幅の決定に使用）</param>
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

        // Managerから該当ステータスの情報を取得
        int maxLv = PlayerManager.instance.GetPlayerIntStatus(maxEnum);
        int currentLv = PlayerManager.instance.GetPlayerIntStatus(currentEnum);

        // // スライダーの全体幅（天井）は「現在の経験値レベル」とする。
        // // ※ただし、アイテム等で局所的にレベル上限が経験値レベルを上回っている場合の安全策として、Mathf.Maxで大きい方を採用する。
        // slider.maxValue = Mathf.Max(expLevel, maxLv);

        slider.maxValue = maxLv;
        slider.minValue = 1;
        slider.value = currentLv;

        // レベル表記の更新
        if (levelText != null)
        {
            levelText.text = $"Lv {currentLv} / {maxLv}";
        }

        // 実際のステータス数値を PlayerStatusLevelManager から取得して表示
        if (statText != null && PlayerManager.instance.StatusLevelManager != null)
        {
            switch (maxEnum)
            {
                case PlayerStatusIntName.hpMaxLevel:
                    statText.text =
                        $"最大HP　　: {PlayerManager.instance.StatusLevelManager.TotalBaseHP}";
                    break;

                case PlayerStatusIntName.attackMaxLevel:
                    statText.text =
                        $"基礎攻撃力: {PlayerManager.instance.StatusLevelManager.TotalBaseAttackPower}";
                    break;

                case PlayerStatusIntName.defenseMaxLevel:
                    statText.text =
                        $"防御力　　: {PlayerManager.instance.StatusLevelManager.TotalBaseDefensePower}";
                    break;

                case PlayerStatusIntName.speedMaxLevel:
                    // SpeedBonusは 0.02 などの「倍率」なので、100倍してパーセントにする
                    statText.text =
                        $"移動速度等: +{PlayerManager.instance.StatusLevelManager.SpeedBonus * 100:F0}%";
                    break;

                case PlayerStatusIntName.luckMaxLevel:
                    // LuckBonusは 2.0 などの「パーセント整数値」なので、そのまま表示する
                    statText.text =
                        $"ドロップ率: +{PlayerManager.instance.StatusLevelManager.LuckBonus:F0}%";
                    break;
            }
        }
    }

    #endregion

    #region ユーザー入力処理 (スライダー操作イベントハンドラ)

    /// <summary>
    /// プレイヤーがマウスやコントローラーでスライダーの値を動かした際に発火するイベント。
    /// レベルの制限（クランプ）や、実際のプレイヤーデータへの反映を行います。
    /// </summary>
    /// <param name="slider">操作されたスライダー本体</param>
    /// <param name="maxEnum">操作対象のステータスの「最大レベル」キー</param>
    /// <param name="currentEnum">操作対象のステータスの「現在レベル」キー</param>
    /// <param name="value">変更後のスライダーの数値（float形式）</param>
    private void OnSliderValueChanged(
        Slider slider,
        PlayerStatusIntName maxEnum,
        PlayerStatusIntName currentEnum,
        float value
    )
    {
        // スクリプトから強制的に値を書き換えた際に呼ばれた場合は、処理を行わず無視する
        if (isUpdatingUI)
            return;

        int maxLevel = PlayerManager.instance.GetPlayerIntStatus(maxEnum);
        int targetValue = Mathf.RoundToInt(value);

        // --- クランプ（制限）処理 ---
        // プレイヤーは「解放済みの最大レベル(maxLevel)」までしかレベルを上げることができない。
        if (targetValue > maxLevel)
            targetValue = maxLevel;
        if (targetValue < 1)
            targetValue = 1;

        int currentLevel = PlayerManager.instance.GetPlayerIntStatus(currentEnum);

        // --- データ更新とUI再描画 ---
        // 実際に値が変動した場合のみ、Managerへ新しいレベルを保存し、全体のUIを再計算・再描画する
        if (currentLevel != targetValue)
        {
            PlayerManager.instance.StatusLevelManager.SetCurrentStatusLevel(
                maxEnum,
                currentEnum,
                targetValue
            );
            RefreshAllUI();
        }

        // --- 視覚的な押し戻し処理 ---
        // スライダーが小数点位置に止まった場合や、上限を突破しようとしてドラッグされた場合、
        // 実際にセット可能な整数値(targetValue)の位置へスライダーのツマミを強制的に戻す。
        if (slider.value != targetValue)
        {
            isUpdatingUI = true;
            slider.value = targetValue;
            isUpdatingUI = false;
        }
    }

    #endregion
}
