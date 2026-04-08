using System;
using NaughtyAttributes;
using UnityEngine;

[CreateAssetMenu(fileName = "TreasureBoxData", menuName = "Game/Treasure Box Data")]
public class TreasureBoxData : ScriptableObject
{
    #region エディタ表示用
    [Tooltip("この宝箱に入っているアイテム（自動更新）")]
    [ReadOnly]
    public string contentLabel; // インスペクター確認用のラベル
    #endregion

    public TreasureBoxName treasureBoxID; // 宝箱のID
    public BaseItemData baseItemData; // 中身のアイテムIDデータ
    public int itemAmount = 1; // アイテムの個数
    #region エディタ専用処理
#if UNITY_EDITOR
    /// <summary>
    /// インスペクター上で値が変更された際に自動的に呼ばれるメソッド。
    /// アイテム名と個数を組み合わせて確認用ラベルを更新します。
    /// </summary>
    private void OnValidate()
    {
        if (baseItemData != null)
        {
            // 例: "回復薬 × 3" のように表示させる
            contentLabel = $"{baseItemData.itemName} × {itemAmount}";
        }
        else
        {
            contentLabel = "未設定 (Empty)";
        }
    }
#endif
    #endregion
}
