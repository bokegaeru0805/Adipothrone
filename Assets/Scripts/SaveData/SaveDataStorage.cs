using System;
using UnityEngine;

/// <summary>
/// セーブスロットに対するES3の読み書きを担当します。
/// </summary>
public sealed class SaveDataStorage
{
    private const string SAVE_DATA_KEY = "SaveData";
    private const string FLAG_DATA_KEY = "FlagSaveKey";
    private const string PLAYER_POSITION_KEY = "PlayerPosition";
    private const string CURRENT_SCENE_NAME_KEY = "CurrentSceneName";
    private const string PLAY_TIME_KEY = "PlayTime";
    private const string PLAYER_EXPERIENCE_KEY = "PlayerEXP";

    private string GetSaveFilePath(int fileNumber)
    {
        return $"Adipothrone_File{fileNumber}.es3";
    }

    public bool TryLoadSlotInfo(
        int fileNumber,
        out SaveSlotInfo slotInfo,
        out int saveSchemaVersion,
        out string errorMessage
    )
    {
        slotInfo = new SaveSlotInfo();
        saveSchemaVersion = 0;
        errorMessage = null;
        string filePath = GetSaveFilePath(fileNumber);

        try
        {
            if (!ES3.FileExists(filePath))
                return true;

            ES3Settings settings = new ES3Settings(filePath);
            slotInfo.playTime = ES3.Load<float>(PLAY_TIME_KEY, defaultValue: 0f, settings);
            if (slotInfo.playTime == 0f)
                return true;

            if (!ES3.KeyExists(SAVE_DATA_KEY, settings))
            {
                slotInfo = new SaveSlotInfo();
                errorMessage = $"スロット {fileNumber} に SaveData キーが存在しません。";
                return false;
            }

            SaveData saveData = ES3.Load<SaveData>(SAVE_DATA_KEY, settings);
            if (saveData?.PlayerStatus == null)
            {
                slotInfo = new SaveSlotInfo();
                errorMessage = $"スロット {fileNumber} の SaveData または PlayerStatus が不正です。";
                return false;
            }

            slotInfo.experience = saveData.PlayerStatus.playerExp;
            saveSchemaVersion = saveData.SaveSchemaVersion;
            return true;
        }
        catch (Exception ex)
        {
            slotInfo = new SaveSlotInfo();
            errorMessage = $"SaveDataの読み込みに失敗（スロット {fileNumber}）: {ex.Message}";
            return false;
        }
    }

    public bool TryLoad(
        int fileNumber,
        Vector2 defaultPlayerPosition,
        string defaultSceneName,
        out SaveGameFileData fileData,
        out string errorMessage
    )
    {
        string filePath = GetSaveFilePath(fileNumber);
        fileData = null;
        errorMessage = null;

        try
        {
            if (!ES3.KeyExists(SAVE_DATA_KEY, filePath))
            {
                errorMessage = "SaveDataのセーブデータが存在しません。";
                return false;
            }

            fileData = new SaveGameFileData
            {
                SaveData = ES3.Load<SaveData>(SAVE_DATA_KEY, filePath, new SaveData()),
                HasFlagData = ES3.KeyExists(FLAG_DATA_KEY, filePath),
                PlayerPosition = ES3.Load<Vector2>(
                    PLAYER_POSITION_KEY,
                    filePath,
                    defaultPlayerPosition
                ),
                SceneName = ES3.Load<string>(
                    CURRENT_SCENE_NAME_KEY,
                    filePath,
                    defaultSceneName
                ),
                PlayTime = ES3.Load<float>(PLAY_TIME_KEY, filePath, 0f),
                PlayerExperience = ES3.Load<int>(PLAYER_EXPERIENCE_KEY, filePath, 0),
            };

            if (fileData.HasFlagData)
            {
                fileData.FlagData = ES3.Load<FlagManager.FlagSaveData>(FLAG_DATA_KEY, filePath);
            }

            return true;
        }
        catch (Exception ex)
        {
            fileData = null;
            errorMessage = $"セーブデータの読み込みに失敗しました: {ex.Message}";
            return false;
        }
    }

    public bool TrySave(int fileNumber, SaveGameFileData fileData, out string errorMessage)
    {
        errorMessage = null;
        if (fileData == null)
        {
            errorMessage = "保存対象のデータがnullです。";
            return false;
        }

        try
        {
            string filePath = GetSaveFilePath(fileNumber);
            ES3.Save(SAVE_DATA_KEY, fileData.SaveData, filePath);
            ES3.Save(FLAG_DATA_KEY, fileData.FlagData, filePath);
            ES3.Save(PLAYER_POSITION_KEY, fileData.PlayerPosition, filePath);
            ES3.Save(CURRENT_SCENE_NAME_KEY, fileData.SceneName, filePath);
            ES3.Save(PLAY_TIME_KEY, fileData.PlayTime, filePath);
            ES3.Save(PLAYER_EXPERIENCE_KEY, fileData.PlayerExperience, filePath);
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = $"セーブデータの保存に失敗しました: {ex.Message}";
            return false;
        }
    }
}
