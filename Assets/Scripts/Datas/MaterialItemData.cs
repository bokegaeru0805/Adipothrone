using UnityEngine;

[CreateAssetMenu(fileName = "MaterialItemData", menuName = "Items/MaterialItem")]
public class MaterialItemData : BaseItemData
{
    public MaterialItemName itemID; //ID

    public override System.Enum GetItemID()
    {
        return itemID;
    }
}
