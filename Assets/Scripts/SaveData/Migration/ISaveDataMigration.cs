/// <summary>
/// SaveDataを1段階更新するマイグレーション処理です。
/// </summary>
public interface ISaveDataMigration
{
    int TargetVersion { get; }

    void Migrate(SaveData saveData);
}
