using System.Collections.Generic;
using NaughtyAttributes;
using UnityEngine;

// ドロップ条件の種類を定義する列挙型
public enum DropConditionType
{
    None, // 条件なし
    KillCountOver, // 指定回数以上の撃破
    PlayerLevelUnder, // プレイヤーレベル以下で討伐
    NoDamage // ノーダメージで討伐
    ,
}

[CreateAssetMenu(fileName = "NewEnemy", menuName = "Enemies/NormalEnemy")]
public class EnemyData : ScriptableObject, IItemIDProvider
{
    public EnemyName enemyID; // 敵のID
    public string enemyName; // 敵の名前

    [ShowAssetPreview]
    [AllowNesting]
    public Sprite encyclopediaSprite; // 図鑑用のスプライト

    [TextArea]
    public string description; // 説明文
    public int enemyHP = 1; // 最大HP
    public int rewardExp; // 倒したときに獲得できる経験値
    public int dropMoney; // 落とす金額
    public int requiredLevel; // 所要レベル
    public List<DropSkillData> dropSkills = new List<DropSkillData>(); // スキルドロップリスト
    public List<DropItemData> dropItems = new List<DropItemData>(); // ドロップアイテムリスト

    public float destroyeffectScale = 1.0f; // 死亡エフェクトの大きさ

    [Tooltip("この敵を図鑑に表示するかどうか")]
    public bool isListedInDex = true;

    public System.Enum GetItemID()
    {
        return enemyID;
    }

#if UNITY_EDITOR
    /// <summary>
    /// インスペクター上で値が変更された際（またはロード時）に自動的に呼ばれるメソッド。
    /// リストの要素名にアイテム名を同期させます。
    /// </summary>
    private void OnValidate()
    {
        if (dropItems != null)
        {
            for (int i = 0; i < dropItems.Count; i++)
            {
                if (dropItems[i] != null)
                {
                    // baseItemData がセットされていれば、その itemName を表示用変数に入れる
                    if (dropItems[i].baseItemData != null)
                    {
                        dropItems[i]._inspectorLabel = dropItems[i].baseItemData.itemName;
                    }
                    else
                    {
                        dropItems[i]._inspectorLabel = "アイテム未設定";
                    }
                }
            }
        }

        if (dropSkills != null)
        {
            for (int i = 0; i < dropSkills.Count; i++)
            {
                if (dropSkills[i] != null)
                {
                    dropSkills[i]._inspectorLabel = dropSkills[i].skillID.ToString();
                }
            }
        }
    }
#endif
}

[System.Serializable]
public class DropItemData
{
    [HideInInspector]
    public string _inspectorLabel; // Unityの仕様を利用してインスペクターの要素名にするための隠し変数

    public BaseItemData baseItemData; // アイテムID(種類が多様なのでEnumにしてはいけない)

    [Range(0f, 100f)]
    public float dropChance; // ドロップ確率（％）

    [Min(1)]
    public int maxDropCount = 1; // 最大ドロップ数

    [Header("Unlock Condition")]
    [Tooltip("ドロップに特殊な解禁条件を設けるか")]
    public bool hasCondition = false;

    [Tooltip("条件の種類")]
    [AllowNesting]
    [ShowIf(nameof(hasCondition))]
    public DropConditionType conditionType;

    [Tooltip("条件の閾値（撃破数、レベルなど）")]
    [AllowNesting]
    [ShowIf(nameof(isShowConditionValue))]
    public int conditionValue;

    private bool isShowConditionValue()
    {
        return hasCondition
            && (
                conditionType == DropConditionType.KillCountOver
                || conditionType == DropConditionType.PlayerLevelUnder
            );
    }

    [Header("Unique Settings")]
    [Tooltip("一度しか入手できない貴重品か（取得即セーブ＆重複ドロップ防止）")]
    public bool isUnique = false;
}

[System.Serializable]
public class DropSkillData
{
    [HideInInspector]
    public string _inspectorLabel; // Unityの仕様を利用してインスペクターの要素名にするための隠し変数

    public SkillName skillID; // ドロップするスキル

    [Range(0f, 100f)]
    public float dropChance; // ドロップ確率（％）
}
