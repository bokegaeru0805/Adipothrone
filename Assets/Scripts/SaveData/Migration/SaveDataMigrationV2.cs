using System;

/// <summary>
/// 従来のver 1.2.1相当のデータ更新を行います。
/// </summary>
public sealed class SaveDataMigrationV2 : ISaveDataMigration
{
    public int TargetVersion => 2;

    public void Migrate(SaveData saveData)
    {
        if (saveData?.PlayerStatus == null)
            throw new InvalidOperationException("SaveDataまたはPlayerStatusがnullです。");

        int playerLevel = PlayerLevelManager.GetLevelFromExp(saveData.PlayerStatus.playerExp);

        if (saveData.PlayerStatus.hpMaxLevel < playerLevel)
        {
            saveData.PlayerStatus.hpMaxLevel = playerLevel;
            saveData.PlayerStatus.hpCurrentLevel = playerLevel;
        }

        if (saveData.PlayerStatus.attackMaxLevel < playerLevel)
        {
            saveData.PlayerStatus.attackMaxLevel = playerLevel;
            saveData.PlayerStatus.attackCurrentLevel = playerLevel;
        }

        if (saveData.PlayerStatus.defenseMaxLevel < playerLevel)
        {
            saveData.PlayerStatus.defenseMaxLevel = playerLevel;
            saveData.PlayerStatus.defenseCurrentLevel = playerLevel;
        }

        saveData.TipsData.RegisterTipsData(TipsName.StatusLevel);
    }
}
