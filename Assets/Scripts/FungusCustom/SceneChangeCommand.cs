using Fungus;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// GameConstantsに定義されたシーンへ非同期で遷移するFungusコマンド。
/// オプションで、遷移先のシーンでのプレイヤー出現位置も設定可能です。
/// </summary>
[CommandInfo(
    "Flow",
    "Scene Change",
    "GameConstantsで定義されたシーンへ非同期遷移します。出現位置の指定も可能です。"
)]
[AddComponentMenu("")]
public class SceneChangeCommand : Command
{
    // Inspectorで選択するためのEnum
    public enum SceneType
    {
        Title, // SCENE_NAME_TITLE
        TutorialStart, // SCENE_NAME_TUTORIAL_START
        Chapter1, // SCENE_NAME_CHAPTER_1
        Desert, // SCENE_NAME_DESERT
        ROYAL_CAPITAL, // SCENE_NAME_ROYAL_CAPITAL
        SNOWFIELD // SCENE_NAME_SNOWFIELD
    }

    [Tooltip("遷移先のシーンを選択")]
    [SerializeField]
    protected SceneType targetScene = SceneType.Title;

    [Tooltip("次のシーンでのプレイヤー出現位置を設定するかどうか")]
    [SerializeField]
    protected bool setSpawnPoint = true;

    [Tooltip("設定したい出現座標 (Vector3Dataですが、XとYのみ使用します)")]
    [SerializeField, ShowIf("setSpawnPoint")]
    [AllowNesting]
    protected Vector3Data spawnPosition;

    public override void OnEnter()
    {
        // 1. シーン名の取得
        string sceneName = GetSceneNameFromEnum(targetScene);

        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError(
                $"SceneChangeCommand: シーン名が取得できませんでした。Enum: {targetScene}"
            );
            Continue();
            return;
        }

        // 2. 出現位置の設定 (setSpawnPointがtrueの場合のみ)
        if (setSpawnPoint)
        {
            if (GameManager.instance != null)
            {
                // Vector3Dataの値をVector2に変換 (Z軸は無視)
                Vector2 pos = new Vector2(spawnPosition.Value.x, spawnPosition.Value.y);
                GameManager.instance.SetCrossSceneSpawnPoint(pos);
            }
            else
            {
                Debug.LogWarning(
                    "SceneChangeCommand: GameManagerが見つからないため、SpawnPointを設定できませんでした。"
                );
            }
        }

        // 3. 非同期ロードを開始
        SceneManager.LoadSceneAsync(sceneName);
    }

    /// <summary>
    /// Enumに対応するシーン名をGameConstantsから取得する
    /// </summary>
    private string GetSceneNameFromEnum(SceneType type)
    {
        switch (type)
        {
            case SceneType.Title:
                return GameConstants.SCENE_NAME_TITLE;
            case SceneType.TutorialStart:
                return GameConstants.SCENE_NAME_TUTORIAL_START;
            case SceneType.Chapter1:
                return GameConstants.SCENE_NAME_CHAPTER_1;
            case SceneType.Desert:
                return GameConstants.SCENE_NAME_DESERT;
            case SceneType.ROYAL_CAPITAL:
                return GameConstants.SCENE_NAME_ROYAL_CAPITAL;
            case SceneType.SNOWFIELD:
                return GameConstants.SCENE_NAME_SNOW;
            default:
                return "";
        }
    }

    public override string GetSummary()
    {
        string summary = $"To: {targetScene}";
        if (setSpawnPoint)
        {
            summary += $" (Spawn: {spawnPosition.Value})";
        }
        return summary;
    }

    public override Color GetButtonColor()
    {
        return new Color32(235, 191, 217, 255);
    }
}
