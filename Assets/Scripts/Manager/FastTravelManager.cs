using System;
using System.Collections; // コルーチンを使用するために必要
using UnityEngine;
using UnityEngine.SceneManagement;

public class FastTravelManager : MonoBehaviour
{
    [Header("ファストトラベルポイントのデータベース")]
    [SerializeField]
    private FastTravelPointDataBase fastTravelPointDataBase; // ファストトラベルポイントのデータベース

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

        // シーンが異なる場合、または死亡リスポーン時（forceReload=true）は
        // 敵やギミックの状態を確実に初期化するため、強制的にシーンロードを行う
        bool isSceneTransition = (sceneName != SceneManager.GetActiveScene().name) || forceReload;

        if (isSceneTransition)
        {
            // ロード完了を待ってからフェードインするため、コルーチンを開始
            StartCoroutine(LoadSceneAndFadeIn(sceneName, selectedFastTravelPoint.targetPosition));
        }
        else
        {
            // 同一シーンで、かつリロード不要な場合（手動トラベルなど）
            DoorOpener.OpenDoor(
                selectedFastTravelPoint.targetPosition,
                this,
                DoorOpener.DoorType.None
            );

            // シーン遷移がないため、即座に完了処理を実行
            OnFastTravelComplete();
        }
    }

    /// <summary>
    /// シーンを非同期でロードし、完了を待機してからフェードインを行います。
    /// </summary>
    /// <param name="sceneName">ロードするシーン名</param>
    /// <param name="targetPos">プレイヤーの移動先座標</param>
    private IEnumerator LoadSceneAndFadeIn(string sceneName, Vector3 targetPos)
    {
        // 次のシーンでのプレイヤーのスポーンポイントを設定
        GameManager.instance.crossScenePlayerSpawnPoint = targetPos;

        // シーンの非同期ロードを開始
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);

        // ロードが完了するまで待機（暗転中の裏読み込み）
        while (!asyncLoad.isDone)
        {
            yield return null;
        }

        // ロード完了後、念のため1フレーム待機して初期化漏れを防ぐ
        yield return null;

        // ロード完了を確認してからフェードインを開始
        FadeCanvas.instance.FadeIn(1f / 60f);

        Debug.Log($"ファストトラベルでシーン遷移完了: {sceneName} に移動しました");

        // 移動完了後の共通処理（チュートリアル等）を実行
        OnFastTravelComplete();
    }

    /// <summary>
    /// ファストトラベル移動完了後に実行される共通処理。
    /// チュートリアルの実行やクールダウンの設定を行います。
    /// </summary>
    private void OnFastTravelComplete()
    {
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