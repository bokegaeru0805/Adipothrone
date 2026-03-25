using UnityEngine;

[CreateAssetMenu(fileName = "TipsData", menuName = "Game/Tips")]
public class TipsInfoData : ScriptableObject
{
    public TipsName tipsName; // Tipsの名前

    [TextArea(1, 3)]
    public string tipsTitle; // Tipsのタイトル
    public Sprite tipsImage; // Tipsの画像

    [TextArea(3, 10)]
    public string tipsText; // 実際の文章
}
