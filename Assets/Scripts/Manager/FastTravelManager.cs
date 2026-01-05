using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class FastTravelManager : MonoBehaviour
{
    [Header("ファストトラベルポイントのデータベース")]
    [SerializeField]
    private FastTravelPointDataBase fastTravelPointDataBase; //ファストトラベルポイントのデータベース

    [Header("デフォルトのファストトラベルポイントID")]
    [SerializeField]
    private FastTravelName defaultFastTravelPointID; // デフォルトのファストトラベルポイントID
    private bool shouldRunDeathFastTravelTutorial = false; // 死亡ファストトラベルチュートリアルを実行するかどうか

    private void Awake()
    {
        if (fastTravelPointDataBase == null)
        {
            Debug.LogError("ファストトラベルポイントのデータベースが設定されていません");
            return;
        }
    }

    /// <summary>
    /// ファストトラベルポイントのデータをIDから取得します。
    /// </summary>
    /// <param name="fastTravelName">ファストトラベルポイントの名前</param>
    /// <returns>対応するファストトラベルポイントのデータ</returns>
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

    /// <summary>
    /// 指定されたファストトラベルポイントへ移動します。
    /// </summary>
    /// <param name="fastTravelID">移動先のID</param>
    /// <param name="forceReload">trueの場合、同一シーンでも強制的にリロードします（死亡時の状態リセット用）</param>
    public void ExecuteFastTravel(Enum fastTravelID, bool forceReload = false)
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

        // 設計変更: シーンが異なる場合、または死亡リスポーン時（forceReload=true）は
        // 敵やギミックの状態を確実に初期化するため、強制的にシーンロードを行う
        if (sceneName != SceneManager.GetActiveScene().name || forceReload)
        {
            // プレイヤーのスポーンポイントを設定
            GameManager.instance.crossScenePlayerSpawnPoint =
                selectedFastTravelPoint.targetPosition;
            // シーンをロード
            SceneManager.LoadScene(sceneName);
        }
        else
        {
            // 同一シーンで、かつリロード不要な場合（手動トラベルなど）
            DoorOpener.OpenDoor(
                selectedFastTravelPoint.targetPosition,
                this,
                DoorOpener.DoorType.None
            );

            // もしリロードしない方針を貫くなら、ここで
            // SceneManager.GetActiveScene().GetRootGameObjects() から
            // IEnemyResettable を探して全リセットする処理が必要になります。
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

        GameManager.instance.TriggerJumpCooldown(); // 会話終了後のジャンプ入力クールダウンを開始
    }

    /// <summary>
    /// 死亡時のファストトラベルを実行します。
    /// </summary>
    public void ExecuteDeathFastTravel()
    {
        // チュートリアルを実行するかどうかを判定
        shouldRunDeathFastTravelTutorial =
            SceneManager.GetActiveScene().name != GameConstants.SCENE_NAME_TUTORIAL_START
            && !FlagManager.instance.GetBoolFlag(TutorialEvent.DeathFastTravelTutorialComplete);

        // 最後に使用したファストトラベルポイントIDを取得
        int lastUsedFastTravelID = GameManager
            .instance
            .savedata
            .FastTravelData
            .LastUsedFastTravelID;
        FastTravelName selectedFastTravelID = (FastTravelName)lastUsedFastTravelID;

        // 最後に使用したファストトラベルポイントが無効な場合、デフォルトのポイントに設定
        if (selectedFastTravelID == FastTravelName.None)
        {
            selectedFastTravelID = defaultFastTravelPointID;
        }

        // 死亡時は強制リロードを有効にして呼び出し、盤面をリセットする
        ExecuteFastTravel(selectedFastTravelID, forceReload: true);
    }
}
