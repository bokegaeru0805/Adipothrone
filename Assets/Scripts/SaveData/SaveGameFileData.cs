using UnityEngine;

/// <summary>
/// ES3内の個別キーを、ロード処理中にまとめて受け渡すためのデータです。
/// 保存ファイル自体の形式は変更しません。
/// </summary>
public sealed class SaveGameFileData
{
    public SaveData SaveData { get; set; }
    public FlagManager.FlagSaveData FlagData { get; set; }
    public bool HasFlagData { get; set; }
    public Vector2 PlayerPosition { get; set; }
    public string SceneName { get; set; }
    public float PlayTime { get; set; }
    public int PlayerExperience { get; set; }
}
