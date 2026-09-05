using UnityEngine;

/// <summary>
/// セーブスロットに依存しないゲーム設定とデバッグ設定を保存・読込します。
/// </summary>
public sealed class SaveLoadSettingsStorage
{
    private const string SETTINGS_FILE_PATH = "GameSettings.es3";
    private const string DEBUG_SETTINGS_FILE_PATH = "DebugSettings.es3";
    private const string SETTINGS_KEY = "settings";

    public GameSettingsSaveData Settings { get; private set; }
    public DebugSettingsSaveData DebugSettings { get; private set; }

    public void LoadSettings()
    {
        bool settingsExist = ES3.KeyExists(SETTINGS_KEY, SETTINGS_FILE_PATH);
        Settings = ES3.Load<GameSettingsSaveData>(
            SETTINGS_KEY,
            SETTINGS_FILE_PATH,
            new GameSettingsSaveData()
        );

        if (settingsExist)
            return;

        Debug.Log("設定ファイルが見つからなかったため、新しい設定ファイルを生成し、保存しました。");
        SaveSettings();
    }

    public void SaveSettings()
    {
        ES3.Save(SETTINGS_KEY, Settings, SETTINGS_FILE_PATH);
    }

    public void LoadDebugSettings()
    {
        bool settingsExist = ES3.KeyExists(SETTINGS_KEY, DEBUG_SETTINGS_FILE_PATH);
        DebugSettings = ES3.Load<DebugSettingsSaveData>(
            SETTINGS_KEY,
            DEBUG_SETTINGS_FILE_PATH,
            new DebugSettingsSaveData()
        );

        if (!settingsExist)
        {
            DebugSettings.isShowEventArea = PlayerPrefs.GetInt("ShowEventArea", 0) == 1;
            DebugSettings.debugTimeScale = PlayerPrefs.GetFloat("DebugTimeScale", 1f);
        }

        DebugSettings.Validate();

        if (!settingsExist)
            SaveDebugSettings();
    }

    public void SaveDebugSettings()
    {
        if (DebugSettings == null)
            return;

        DebugSettings.Validate();
        ES3.Save(SETTINGS_KEY, DebugSettings, DEBUG_SETTINGS_FILE_PATH);
    }
}
