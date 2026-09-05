/// <summary>
/// 従来のver 1.2.0相当のデータ更新を行います。
/// </summary>
public sealed class SaveDataMigrationV1 : ISaveDataMigration
{
    public int TargetVersion => 1;

    public void Migrate(SaveData saveData)
    {
        if (saveData.EnemyRecordData?.enemyRecords == null)
            return;

        foreach (var entry in saveData.EnemyRecordData.enemyRecords)
        {
            if (entry == null)
                continue;

            entry.hasEncountered = true;
        }
    }
}
