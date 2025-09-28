using System.Collections.Generic;
using Effekseer;
using UnityEngine;

[CreateAssetMenu(fileName = "NewEnemy", menuName = "Enemies/NormalEnemy")]
public class EnemyData : ScriptableObject
{
    public EnemyName enemyID; // 敵のID
    public string enemyName; // 敵の名前
    public Sprite encyclopediaSprite; // 図鑑用のスプライト

    [TextArea]
    public string description; // 説明文
    public int enemyHP; // 最大HP
    public int rewardExp; // 倒したときに獲得できる経験値
    public int dropMoney; // 落とす金額
    public int requiredLevel; // 所要レベル
    public List<DropItemData> dropItems = new List<DropItemData>(); // ドロップアイテムリスト
    public EffekseerEmitter destroyeffect; // 死亡エフェクトのアセット
    public float destroyeffectScale = 1.0f; // 死亡エフェクトの大きさ
    [Tooltip("この敵を図鑑に表示するかどうか")]
    public bool isListedInDex = true;

}

[System.Serializable]
public class DropItemData
{
    public BaseItemData baseItemData; // アイテムID(種類が多様なのでEnumにしてはいけない)

    [Range(0f, 100f)]
    public float dropChance; // ドロップ確率（％）

    [Min(1)]
    public int maxDropCount = 1; // 最大ドロップ数

    [Tooltip("アイテムドロップに対する幸運のボーナス倍率")]
    [BonusMultiplierPopup]
    [SerializeField]
    public float luckBonusMultiplier = 1.0f; // 幸運のボーナス倍率
    //ドロップ率の効果を追加するにはBonusMultiplierPopupDrawerを編集してください
}
