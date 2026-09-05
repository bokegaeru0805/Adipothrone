using System;
using System.Collections.Generic;

/// <summary>
/// SaveDataのマイグレーションを古い順に実行します。
/// </summary>
public static class SaveDataMigrationRunner
{
    public const int CurrentVersion = 3;

    private static readonly IReadOnlyList<ISaveDataMigration> Migrations =
        new ISaveDataMigration[]
        {
            new SaveDataMigrationV1(),
            new SaveDataMigrationV2(),
            new SaveDataMigrationV3(),
        };

    public static void MigrateToCurrent(SaveData saveData)
    {
        if (saveData == null)
            throw new ArgumentNullException(nameof(saveData));

        int sourceVersion = GetSourceVersion(saveData);
        if (sourceVersion > CurrentVersion)
        {
            throw new InvalidOperationException(
                $"未対応のSaveSchemaVersionです。(データ: {sourceVersion}, ゲーム: {CurrentVersion})"
            );
        }

        saveData.SaveSchemaVersion = sourceVersion;

        foreach (var migration in Migrations)
        {
            if (saveData.SaveSchemaVersion >= migration.TargetVersion)
                continue;

            migration.Migrate(saveData);
            saveData.SaveSchemaVersion = migration.TargetVersion;
        }
    }

    private static int GetSourceVersion(SaveData saveData)
    {
        if (saveData.SaveSchemaVersion > 0)
            return saveData.SaveSchemaVersion;

        // SaveSchemaVersion導入前のデータだけ、旧GameVersionから移行済み段階を推定する。
        if (!Version.TryParse(saveData.GameVersion, out Version gameVersion))
            return 0;

        if (gameVersion >= new Version("1.3.0"))
            return 3;
        if (gameVersion >= new Version("1.2.1"))
            return 2;
        if (gameVersion >= new Version("1.2.0"))
            return 1;

        return 0;
    }
}
