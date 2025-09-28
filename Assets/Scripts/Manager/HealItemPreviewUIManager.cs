using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HealItemPreviewUIManager : MonoBehaviour
{
    public static HealItemPreviewUIManager instance { get; private set; }
    private PlayerEffectManager playerEffectManager; // プレイヤーの効果を管理するマネージャー
    private PlayerManager playerManager; // プレイヤーのマネージャー

    [SerializeField]
    private HealItemDatabase healItemDatabase; // アイテムデータベース

    [Header("エフェクトを示すアイコン")]
    [SerializeField]
    private List<BuffIconSet> buffIconSets; // バフアイコンのセット

    [System.Serializable]
    public class BuffIconSet
    {
        public StatusEffectType type;
        public Sprite highTimeIcon; // 残り時間が多いとき
        public Sprite midTimeIcon; // 中間
        public Sprite lowTimeIcon; // 残り時間が少ないとき
    }

    private int playerMaxHP = 0; // プレイヤーの最大HP
    private int playerMaxWP = 0; // プレイヤーの最大WP
    private int attackBuffLimit = 0; // 攻撃力バフの上限
    private int defenceBuffLimit = 0; // 防御力バフの上限
    private int speedBuffLimit = 0; // スピードバフの上限
    private int luckBuffLimit = 0; // 運バフの上限
    private float attackRemainingTime = 0; // 攻撃力バフの残り時間
    private float attackDeltaValue = 0; // 攻撃力バフの増加量
    private float defenseRemainingTime = 0; // 防御力バフの残り時間
    private float defenseDeltaValue = 0; // 防御力バフの増加量
    private float speedRemainingTime = 0; // スピードバフの残り時間
    private float speedDeltaValue = 0; // スピードバフの増加量
    private float luckRemainingTime = 0; // 運バフの残り時間
    private float luckDeltaValue = 0; // 運バフの増加量
    private Dictionary<StatusEffectType, BuffIconSet> buffIconLookup; // バフアイコンの辞書

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            if (healItemDatabase == null)
            {
                Debug.LogError(
                    "アイテムの効果を示すUIManagerにHealItemDatabaseが設定されていません"
                );
            }

            if (
                buffIconSets == null
                || buffIconSets.Count == 0
                || buffIconSets.Any(s =>
                    s.highTimeIcon == null || s.midTimeIcon == null || s.lowTimeIcon == null
                )
            )
            {
                Debug.LogError("バフアイコンのセットが正しく設定されていません");
            }

            buffIconLookup = buffIconSets.ToDictionary(s => s.type); // バフアイコンのセットを辞書に変換
        }
        else
        {
            Destroy(gameObject); // 既存のインスタンスがある場合は破棄
        }
    }

    private void Update()
    {
        LoadPlayerEffectStates(); // プレイヤーの効果状態をセーブデータから読み込む
    }

    private void Start()
    {
        playerEffectManager = PlayerEffectManager.instance;
        if (playerEffectManager != null)
        {
            //バフの上限を取得する関数をイベントに登録
            playerEffectManager.OnChangeBuffLimit += LoadBuffLimitLevels;
            LoadBuffLimitLevels(); //バフの上限を取得
        }
        else
        {
            Debug.LogError(
                "PlayerEffectManagerが見つかりませんでした。回復アイテムのUIが正しく機能しません。"
            );
        }

        playerManager = PlayerManager.instance;
        if (playerManager != null)
        {
            playerManager.OnChangeMaxHP += LoadMaxHP; // 最大HPが変化したときに呼び出されるイベントを登録
            LoadMaxHP(playerManager.playerMaxHP); // 初期の最大HPを取得
            playerManager.OnChangeMaxWP += LoadMaxWP; // 最大WPが変化したときに呼び出されるイベントを登録
            LoadMaxWP(playerManager.playerMaxWP); // 初期の最大WPを取得
        }
    }

    /// <summary>
    /// 指定されたアイテムIDに対応する回復アイテムの効果をUIに反映します。
    /// HP回復量とWP回復量に応じてバーを表示・更新し、
    /// さらに最大2つまでのバフ効果のアイコンとゲージを表示します。
    /// </summary>
    /// <param name="itemID">選択された回復アイテムのID</param>
    /// <param name="playerHPBar">HPバーのGameObject</param>
    /// <param name="playerHPHealthBarImage">HPバーのImage</param>
    /// <param name="playerWPBar">WPバーのGameObject</param>
    /// <param name="playerWPHealthBarImage">WPバーのImage</param>
    /// <param name="specialEffect1Icon">1つ目の特殊効果アイコン</param>
    /// <param name="specialEffect1Bar">1つ目の特殊効果バーのGameObject</param>
    /// <param name="specialEffect1BarImage">1つ目の特殊効果バーのImage</param>
    /// <param name="specialEffect2Icon">2つ目の特殊効果アイコン</param>
    /// <param name="specialEffect2Bar">2つ目の特殊効果バーのGameObject</param>
    /// <param name="specialEffect2BarImage">2つ目の特殊効果バーのImage</param>
    public void DisplaySelectedItemEffects(
        int itemID,
        GameObject playerHPBar,
        Image playerHPHealthBarImage,
        GameObject playerWPBar,
        Image playerWPHealthBarImage,
        Dictionary<GameObject, (GameObject barObj, Image barImage)> buffUIs
    )
    {
        if (itemID == 0)
        {
            // アイテムが無効な場合の処理
            playerHPBar.SetActive(false); // HPバーを非表示
            playerWPBar.SetActive(false); // WPバーを非表示
            foreach (var bar in buffUIs)
            {
                //バフのアイコンとバーを非表示
                bar.Key.SetActive(false);
                bar.Value.barObj.SetActive(false);
            }
            return;
        }
        // アイテムデータを取得
        HealItemData healItemData = healItemDatabase.GetItemByID((HealItemName)itemID);

        int hpHealAmount = healItemData.hpHealAmount; // HP回復量を取得
        int wpHealAmount = healItemData.wpHealAmount; // WP回復量を取得

        //HPバーの表示の設定
        if (hpHealAmount > 0)
        {
            if (!playerHPBar.activeSelf)
            {
                playerHPBar.SetActive(true); // HPバーを表示
            }
            playerHPHealthBarImage.fillAmount = (float)hpHealAmount / (float)playerMaxHP; // HPバーの割合を計算
        }
        else
        {
            if (playerHPBar.activeSelf)
            {
                playerHPBar.SetActive(false); // HPバーを非表示
            }
        }

        //WPバーの表示の設定
        if (wpHealAmount > 0)
        {
            if (!playerWPBar.activeSelf)
            {
                playerWPBar.SetActive(true); // WPバーを表示
            }
            playerWPHealthBarImage.fillAmount = (float)wpHealAmount / (float)playerMaxWP; // WPバーの割合を計算
        }
        else
        {
            if (playerWPBar.activeSelf)
            {
                playerWPBar.SetActive(false); // WPバーを非表示
            }
        }

        //バフ効果のアイコンとバーを初期化
        foreach (var bar in buffUIs)
        {
            bar.Key.SetActive(false);
            bar.Value.barObj.SetActive(false);
        }

        // バフ効果を表示
        for (int i = 0; i < Mathf.Min(healItemData.buffEffects.Count, 2); i++)
        {
            //エフェクトの効果を取得
            var effect = healItemData.buffEffects[i];
            //IconのUIコンポーネントを取得
            var icon = buffUIs.Keys.ElementAt(i);
            //バーのUIコンポーネントを取得
            var (barObj, barImage) = buffUIs[icon];
            //Iconの画像を変更
            icon.GetComponent<Image>().sprite = GetEffectIcon(effect.effectType, effect.effectrank);
            //バーの表示を設定
            barImage.fillAmount = effect.multiplier / GetBuffLimitValue(effect.effectType);
            //Iconとバーを表示
            icon.SetActive(true);
            barObj.SetActive(true);
        }
    }

    /// <summary>
    /// プレイヤーのステータス効果（バフ）に応じて、対応するアイコンおよびバーUIを更新します。
    /// 各バフに対して：
    /// ・残り時間に応じたアイコン（high/mid/low）を設定
    /// ・有効な場合のみバーを表示し、fillAmount を反映
    /// ・残り時間が 1 秒以下のバフについては、buffExpirationFlags に true を設定します。
    /// </summary>
    /// <param name="iconImages">各バフタイプに対応する UIアイコンImage のマップ</param>
    /// <param name="buffBars">各バフタイプに対応するバーオブジェクトとバーImageのマップ</param>
    /// <param name="buffExpirationFlags">残り時間が1秒以下のバフを true とする出力フラグマップ</param>
    public void DisplayPlayerStatusEffect(
        Dictionary<StatusEffectType, Image> iconImages,
        Dictionary<StatusEffectType, (GameObject barObj, Image barImage)> buffBars,
        out Dictionary<StatusEffectType, bool> buffExpirationFlags
    )
    {
        buffExpirationFlags = new Dictionary<StatusEffectType, bool>();

        foreach (var pair in iconImages)
        {
            // バフのタイプに応じた残り時間を取得
            float remainingTime = GetBuffRemainingTime(pair.Key);
            // バフのタイプを取得
            var type = pair.Key;

            // バフのアイコンコンポーネントを取得
            var iconImage = iconImages[type];

            // 対応するバーUIが存在するか確認
            if (buffBars.TryGetValue(type, out var barData))
            {
                GameObject bar = barData.barObj;
                Image barFillImage = barData.barImage;
                float delta = GetBuffDeltaValue(type);
                float limit = GetBuffLimitValue(type);

                if (remainingTime > 0f)
                {
                    if (!bar.activeSelf)
                    {
                        bar.SetActive(true); // バーを表示
                    }
                    barFillImage.fillAmount = delta / limit;
                    if (pair.Value != null)
                    {
                        // アイコンの表示
                        iconImage.gameObject.SetActive(true);
                    }
                    // 登録されているスプライトセットに応じてバフのアイコンを切り替え
                    if (buffIconLookup.TryGetValue(type, out var iconSet))
                    {
                        if (
                            remainingTime
                            > StatusEffectUtility.GetDurationByRank(StatusEffectRank.II)
                        )
                        {
                            iconImage.sprite = iconSet.highTimeIcon;
                        }
                        else if (
                            remainingTime
                            > StatusEffectUtility.GetDurationByRank(StatusEffectRank.I)
                        )
                        {
                            iconImage.sprite = iconSet.midTimeIcon;
                        }
                        else
                        {
                            iconImage.sprite = iconSet.lowTimeIcon;
                        }
                    }
                }
                else
                {
                    if (bar.activeSelf)
                    {
                        bar.SetActive(false); // バーを非表示
                    }
                    if (iconImage.gameObject.activeSelf)
                    {
                        iconImage.gameObject.SetActive(false); // アイコンを非表示
                    }
                }
            }

            // バフの残り時間が僅かなら、フラグを true に設定
            buffExpirationFlags[type] =
                remainingTime <= StatusEffectUtility.GetDurationByRank(StatusEffectRank.I) / 2f;
        }
    }

    /// <summary>
    /// バフの上限の値を、セーブデータから取得する関数
    /// </summary>
    private void LoadBuffLimitLevels()
    {
        if (playerEffectManager != null)
        {
            // PlayerManagerからバフの上限を取得
            attackBuffLimit = playerEffectManager.attackBuffLimitLevel;
            defenceBuffLimit = playerEffectManager.defenceBuffLimitLevel;
            speedBuffLimit = playerEffectManager.speedBuffLimitLevel;
            luckBuffLimit = playerEffectManager.luckBuffLimitLevel;
        }
        else
        {
            Debug.LogError("PlayerEffectManagerが見つかりませんでした");
        }
    }

    //HPの最大値を取得する関数
    private void LoadMaxHP(int newMaxHP)
    {
        // PlayerManagerからHPの最大値を取得
        playerMaxHP = newMaxHP;
        if (playerMaxHP <= 0)
        {
            Debug.LogError("プレイヤーの最大HPが設定されていません");
        }
    }

    //WPの最大値を取得する関数
    private void LoadMaxWP(int newMaxWP)
    {
        // PlayerManagerからWPの最大値を取得
        playerMaxWP = newMaxWP;
        if (playerMaxWP <= 0)
        {
            Debug.LogError("プレイヤーの最大WPが設定されていません");
        }
    }

    private void OnDisable()
    {
        // イベント解除（メモリリーク防止）
        if (playerEffectManager != null)
        {
            playerEffectManager.OnChangeBuffLimit -= LoadBuffLimitLevels;
        }

        if (playerManager != null)
        {
            playerManager.OnChangeMaxHP -= LoadMaxHP;
            playerManager.OnChangeMaxWP -= LoadMaxWP;
        }
    }

    /// <summary>
    /// 指定されたステータス効果のアイコンを取得します。
    /// </summary>
    private Sprite GetEffectIcon(StatusEffectType effectType, StatusEffectRank rank)
    {
        // 登録されているスプライトセットに応じてImage.spriteを切り替え
        if (buffIconLookup.TryGetValue(effectType, out var iconSet))
        {
            switch (rank)
            {
                case StatusEffectRank.I:
                    return iconSet.highTimeIcon;
                case StatusEffectRank.II:
                    return iconSet.midTimeIcon;
                case StatusEffectRank.III:
                    return iconSet.lowTimeIcon;
                default:
                    return null; // デフォルト値
            }
        }

        return null; // デフォルト値
    }

    /// <summary>
    /// 指定されたステータス効果の上限値を取得します。
    /// </summary>
    private float GetBuffLimitValue(StatusEffectType effectType)
    {
        switch (effectType)
        {
            case StatusEffectType.Attack:
                if (attackBuffLimit != 0)
                {
                    return attackBuffLimit;
                }
                break;
            case StatusEffectType.Defense:
                if (defenceBuffLimit != 0)
                {
                    return defenceBuffLimit;
                }
                break;
            case StatusEffectType.Speed:
                if (speedBuffLimit != 0)
                {
                    return speedBuffLimit;
                }
                break;
            case StatusEffectType.Luck:
                if (luckBuffLimit != 0)
                {
                    return luckBuffLimit;
                }
                break;
            default:
                return 1f;
        }

        return 1f; // デフォルト値(分母で用いるので、0は不可)
    }

    /// <summary>
    /// 指定されたステータス効果の残り時間を取得します。
    /// </summary>
    private float GetBuffRemainingTime(StatusEffectType effectType)
    {
        switch (effectType)
        {
            case StatusEffectType.Attack:
                return attackRemainingTime;
            case StatusEffectType.Defense:
                return defenseRemainingTime;
            case StatusEffectType.Speed:
                return speedRemainingTime;
            case StatusEffectType.Luck:
                return luckRemainingTime;
            default:
                return 0f;
        }
    }

    /// <summary>
    /// 指定されたステータス効果の増加量を取得します。
    /// </summary>
    private float GetBuffDeltaValue(StatusEffectType effectType)
    {
        switch (effectType)
        {
            case StatusEffectType.Attack:
                return attackDeltaValue;
            case StatusEffectType.Defense:
                return defenseDeltaValue;
            case StatusEffectType.Speed:
                return speedDeltaValue;
            case StatusEffectType.Luck:
                return luckDeltaValue;
            default:
                return 0f;
        }
    }

    /// <summary>
    /// プレイヤーの効果状態をセーブデータから読み込みます。
    /// </summary>
    private void LoadPlayerEffectStates()
    {
        var effectList = GameManager.instance.savedata.PlayerStatus.playerEffectStates;

        if (effectList == null)
        {
            Debug.LogError("PlayerEffectStatesが見つかりませんでした");
            return;
        }

        //各効果の残り時間と増加量をセーブデータから数値を取得
        foreach (var effect in effectList)
        {
            switch (effect.effectTypeNumber)
            {
                case (int)StatusEffectType.Attack:
                    attackRemainingTime = effect.remainingTime;
                    attackDeltaValue = effect.deltaValue;
                    break;
                case (int)StatusEffectType.Defense:
                    defenseRemainingTime = effect.remainingTime;
                    defenseDeltaValue = effect.deltaValue;
                    break;
                case (int)StatusEffectType.Speed:
                    speedRemainingTime = effect.remainingTime;
                    speedDeltaValue = effect.deltaValue;
                    break;
                case (int)StatusEffectType.Luck:
                    luckRemainingTime = effect.remainingTime;
                    luckDeltaValue = effect.deltaValue;
                    break;
                default:
                    break;
            }
        }
    }
}
