using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class FastTravelManager : MonoBehaviour
{
    [Header("ファストトラベルポイントのデータベース")]
    [SerializeField]
    private FastTravelPointDataBase fastTravelPointDataBase; //ファストトラベルポイントのデータベース
    private bool shouldRunDeathFastTravelTutorial = false; // 死亡ファストトラベルチュートリアルを実行するかどうか

    private void Awake()
    {
        if (fastTravelPointDataBase == null)
        {
            Debug.LogError("ファストトラベルポイントのデータベースが設定されていません");
            return;
        }
    }

    public FastTravelPointData GetFastTravelPointData(FastTravelName fastTravelName)
    {
        if (fastTravelPointDataBase == null)
        {
            Debug.LogError("ファストトラベルポイントのデータベースが設定されていません");
            return null;
        }

        // IDからファストトラベルポイントを取得
        return fastTravelPointDataBase.GetFastTravelPointByID(fastTravelName);
    }

    public void ExecuteFastTravel(Enum fastTravelID)
    {
        // 選択されたファストトラベルIDを取得
        FastTravelName selectedFastTravelID = (FastTravelName)fastTravelID;

        // 選択されたファストトラベルポイントのデータを取得
        FastTravelPointData selectedFastTravelPoint =
            fastTravelPointDataBase.GetFastTravelPointByID(selectedFastTravelID);
        if (selectedFastTravelPoint == null)
        {
            Debug.LogError(
                $"選択されたファストトラベルポイントが見つかりません: {selectedFastTravelID}"
            );
            return;
        }

        PlayerManager.instance.RestoreFullHP(); // プレイヤーのHPを全回復

        string sceneName = selectedFastTravelPoint.GetSceneName(); // 移動先のシーン名を取得
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("移動先のシーン名が設定されていません");
            return;
        }

        if (sceneName != SceneManager.GetActiveScene().name)
        {
            // プレイヤーのスポーンポイントを設定
            GameManager.instance.crossScenePlayerSpawnPoint =
                selectedFastTravelPoint.targetPosition;
            // シーンが異なる場合はシーンをロード
            SceneManager.LoadScene(sceneName);
        }
        else
        {
            // 同じシーン内でのファストトラベルの場合は、プレイヤーの位置を更新
            DoorOpener.OpenDoor(
                selectedFastTravelPoint.targetPosition,
                this,
                DoorOpener.DoorType.None // ドアの種類は特に指定しない
            );
        }

        if (shouldRunDeathFastTravelTutorial)
        {
            FlagManager.instance.SetBoolFlag(TutorialEvent.DeathFastTravelTutorialComplete, true); // チュートリアル完了フラグを設定
            FungusHelper.ExecuteBlock(
                GlobalFlowchartController.instance.globalFlowchart,
                "DeathFastTravelTutorial"
            ); // 死亡ファストトラベルチュートリアルを実行
            shouldRunDeathFastTravelTutorial = false; // チュートリアル実行フラグをリセット
        }
    }

    public void ExecuteDeathFastTravel()
    {
        shouldRunDeathFastTravelTutorial =
            SceneManager.GetActiveScene().name != GameConstants.SceneName_TutorialStart
            && !FlagManager.instance.GetBoolFlag(TutorialEvent.DeathFastTravelTutorialComplete);

        int lastUsedFastTravelID = GameManager
            .instance
            .savedata
            .FastTravelData
            .LastUsedFastTravelID;
        FastTravelName selectedFastTravelID = (FastTravelName)lastUsedFastTravelID;
        ExecuteFastTravel(selectedFastTravelID);
        SEManager.instance.PlayPlayerActionSE(SE_PlayerAction.Death1); // 死亡時のSEを再生
    }
}
