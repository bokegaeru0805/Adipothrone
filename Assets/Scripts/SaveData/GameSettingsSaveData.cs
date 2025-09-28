[System.Serializable]
public class GameSettingsSaveData
{
    public float bgmVolume; // BGM音量 (0.0f 〜 1.0f)
    public float seVolume; // SE音量 (0.0f 〜 1.0f)
    public int lastUsedSlotIndex; // 最後に使用したセーブスロット番号 (1=スロット1, 2=スロット2, ...)
    public bool isShowingControlsGuide; // 画面上に操作ガイドを表示するかどうか

    // ゲーム初回起動時のデフォルト値を設定
    public GameSettingsSaveData()
    {
        bgmVolume = 1.0f;
        seVolume = 1.0f;
        // デフォルトはスロット1
        // 0にすると配列の添字と一致しなくなるため、1に設定
        lastUsedSlotIndex = 1;
        isShowingControlsGuide = true;
    }
}