using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 各セーブスロットの概要情報（プレイ時間と経験値）を格納するクラス
/// </summary>
[System.Serializable]
public class SaveSlotInfo
{
    public float playTime; // プレイ時間（秒）
    public int experience; // 経験値

    // コンストラクタ（初期値を設定しやすくするため）
    public SaveSlotInfo(float time = 0f, int exp = 0)
    {
        playTime = time;
        experience = exp;
    }
}

public class SaveLoadManager : MonoBehaviour
{
    public static SaveLoadManager instance { get; private set; } //シングルトンインスタンス
    private const int DEBUG_LOAD_SLOT_NUMBER = -1; //デバッグ用のロードスロット番号
    private Vector2 PlayerStartPos = new Vector2(-110, 0); //プレイヤーの初期座標
    private readonly SaveDataStorage _saveDataStorage = new SaveDataStorage();
    private readonly SaveLoadSettingsStorage _settingsStorage = new SaveLoadSettingsStorage();
    public GameSettingsSaveData Settings => _settingsStorage.Settings;
    public DebugSettingsSaveData DebugSettings => _settingsStorage.DebugSettings;
    public static float timeSinceLoad; //ロードしてからのプレイ時間を保存する変数
    public static float StartTime; //始まるまでのプレイ時間を保存する変数
    public SaveLoadMode CurrentSaveLoadMode { get; private set; } = SaveLoadMode.Load; //セーブロードの状態を管理する変数
    public event Action<bool> OnEnableSaveStateChanged; //セーブ可能状態が変化したときに呼び出されるイベント
    public static event Action<bool> OnLoadingStateChanged; // ロード状態が変化したことを通知するstaticイベントを追加
    private float _timeSinceLastSave = 0f; // 前回のセーブからの経過時間（ゲーム内時間）

    public enum SaveLoadMode
    {
        None = 0, //何もしない
        Save = 1,
        Load = 2,
    }

    //セーブデータのプレイ時間などの情報を保存する辞書
    public static Dictionary<int, SaveSlotInfo> FileSlotInfos;

    // 非公開の読み書き用フラグ（このインスタンスが現在ロード中かどうか）
    private bool isLoading = false;

    // 外部から参照可能な読み取り専用プロパティ（現在のロード状態）
    public static bool IsLoading
    {
        get
        {
            if (instance == null)
            {
                Debug.LogWarning("SaveLoadManagerが存在しません。ロード状態を取得できません。");
                return false;
            }
            return instance.isLoading;
        }
    }
    public bool isEnableSave { get; private set; } = false; //セーブをできるかどうかを調べる
    public static bool isOnSave; //セーブ待機中かどうかのフラグ
    public static bool isDataPrompting; //データ変更画面が開いているかのフラグ

    /// <summary>
    /// アクティブなUI Managerを積み重ねるスタック。
    /// </summary>
    private static Stack<IPanelStackManager> managerStack = new Stack<IPanelStackManager>();

    /// <summary>
    /// 現在アクティブな、パネルスタックを管理するManager (スタックの一番上のManager)
    /// </summary>
    public static IPanelStackManager CurrentActiveManager
    {
        get
        {
            // スタックが空でなければ一番上を返し、空なら null を返す
            return managerStack.Count > 0 ? managerStack.Peek() : null;
        }
    }

    /// <summary>
    /// 現在アクティブなパネルスタックManagerをスタックに登録（Push）します。
    /// 主に各UI Manager (UIManager, TitleUIManagerなど) のAwake()から呼び出されます。
    /// </summary>
    /// <param name="manager">登録するManager</param>
    public static void RegisterActiveManager(IPanelStackManager manager)
    {
        if (manager == null)
        {
            Debug.LogError("null の Manager を登録しようとしました。");
            return;
        }

        // Debug.Log($"[{manager.GetType().Name}] がアクティブManagerとしてスタックに登録されました。");
        managerStack.Push(manager);
    }

    /// <summary>
    /// Managerが破棄される際にスタックから登録解除（Pop）します。
    /// </summary>
    /// <param name="manager">登録解除するManager</param>
    public static void UnregisterActiveManager(IPanelStackManager manager)
    {
        if (manager == null)
        {
            Debug.LogError("null の Manager を登録解除しようとしました。");
            return;
        }

        // スタックが空、または一番上のManagerが自分でない場合 (何らかの異常)
        if (managerStack.Count == 0 || managerStack.Peek() != manager)
        {
            Debug.LogError(
                $"Managerスタックの登録解除エラー: "
                    + $"解除しようとした [{manager.GetType().Name}] はスタックの一番上にいません。",
                manager as MonoBehaviour
            );

            // 【堅牢化処理】スタック内に存在する場合は、強制的に取り除く
            // ※通常は起こらないはずだが、安全のため
            if (managerStack.Contains(manager))
            {
                // スタックをリストに変換し、該当アイテムを削除して、スタックを再構築
                var tempList = managerStack.ToList();
                tempList.Remove(manager);
                tempList.Reverse(); // StackはLIFOなので、逆順にしてからClear & Push
                managerStack.Clear();
                foreach (var item in tempList)
                {
                    managerStack.Push(item);
                }
                Debug.LogWarning(
                    $"[{manager.GetType().Name}] をスタックの途中から強制的に削除しました。"
                );
            }
            return;
        }

        // 正常な解除処理
        var poppedManager = managerStack.Pop();
        // Debug.Log($"[{poppedManager.GetType().Name}] がスタックから登録解除されました。");

        // if (CurrentActiveManager != null)
        // {
        //     Debug.Log($"アクティブManagerは [{CurrentActiveManager.GetType().Name}] に戻りました。");
        // }
        // else
        // {
        //     Debug.Log("アクティブなManagerスタックは空になりました。");
        // }
    }

    /// <summary>
    /// シングルトン初期化
    /// </summary>
    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            //DontDestroyOnLoad(this.gameObject); //他のManagerがStartで必要とするため、Awakeで取得する
        }
        else
        {
            Destroy(this.gameObject);
            return;
        }

        LoadSettings(); // ゲーム起動時に必ず設定ファイルを読み込む
        LoadDebugSettings();

        FileSlotInfos = new Dictionary<int, SaveSlotInfo>(); //ゲームのプレイ時間などの情報を保存する変数を初期化
        isOnSave = false; //セーブ待機中のフラグを初期化

        if (FileSlotInfos == null)
        {
            Debug.LogWarning("FileSlotInfosが初期化されていません。");
            return;
        }
        else
        {
            // --- Awakeメソッド内のforループ部分 ---
            for (
                int i = GameConstants.AUTO_SAVE_FILE_NUMBER; // オートセーブスロット番号から開始
                // 通常のセーブスロット数 + オートセーブスロット数 までループ
                i < GameConstants.MaxSaveLoadFiles + GameConstants.MAX_AUTOSAVE_FOLDERS;
                i++
            )
            {
                bool isValid = _saveDataStorage.TryLoadSlotInfo(
                    i,
                    out SaveSlotInfo slotInfo,
                    out int saveSchemaVersion,
                    out string errorMessage
                );

                if (!isValid)
                {
                    Debug.LogWarning($"{errorMessage} データを無効として扱います。");
                }

                if (
                    slotInfo.playTime > 0f
                    && saveSchemaVersion > SaveDataMigrationRunner.CurrentVersion
                )
                {
                    Debug.LogWarning(
                        $"スロット {i} のセーブデータ形式は新しいためロードできません。"
                            + $"(データ: {saveSchemaVersion}, ゲーム: {SaveDataMigrationRunner.CurrentVersion})"
                    );
                    slotInfo = new SaveSlotInfo();
                }

                FileSlotInfos.Add(i, slotInfo);
            }
        }
    }

    private void Start()
    {
#if UNITY_EDITOR
        // エディタ実行時かつ、初回起動であり、現在のシーンがタイトルでない場合
        if (
            !GameManager.isFirstGameOpen
            && SceneManager.GetActiveScene().name != GameConstants.SCENE_NAME_TITLE
        )
        {
            // 即座に実行せず、1フレーム待機するコルーチンを呼び出す
            StartCoroutine(DebugLoadSequence(DEBUG_LOAD_SLOT_NUMBER));
            // Debug.Log(
            //     $"<color=yellow>[Debug]</color> タイトル以外のシーンから開始されました。スロット {DEBUG_LOAD_SLOT_NUMBER} をロードして開始します（シーン遷移なし）。"
            // );
            return; // 通常のStart処理は行わない
        }
#endif

        if (!GameManager.isFirstGameOpen)
        {
            //初めてゲームが開かれたとき
            GameManager.instance?.ResetState(); //ゲーム内の変数を初期化
            GameManager.isFirstGameOpen = true; //初回起動フラグを立てる
            SetToLoadMode(); //ロード状態にする

            if (Settings == null)
            {
                Debug.LogWarning("SaveLoadManagerのSettingsがnullです。");
            }
            else
            {
                if (BGMManager.instance == null)
                {
                    Debug.LogWarning("BGMManagerが存在しません");
                }
                else
                {
                    BGMManager.instance.AdjustAllVolume(Settings.bgmVolume); //BGM音量を設定
                }

                if (SEManager.instance == null)
                {
                    Debug.LogWarning("SEManagerが存在しません");
                }
                else
                {
                    SEManager.instance.AdjustAllSEVolume(Settings.seVolume); //SE音量を設定
                }
            }
        }
    }

    private void Update()
    {
        // 時間経過を記録
        // Time.deltaTimeはtimeScaleの影響を受けるため、ポーズ中は加算されない
        _timeSinceLastSave += Time.deltaTime;
    }

    /// <summary>
    /// セーブまたはロードを実行するコルーチン
    /// </summary>
    public IEnumerator SaveLoad(int file_number, bool loadScene = true)
    {
        if (CurrentSaveLoadMode == SaveLoadMode.Save)
        {
            PerformSave(file_number);
            yield break; //セーブ処理は同期的なのでここでコルーチンを終了
        }

        if (CurrentSaveLoadMode != SaveLoadMode.Load)
            yield break;

        PlayerManager playerManagerOnLoadStart = BeginLoad();
        SaveGameFileData fileData = null;

        if (file_number != GameConstants.NEW_GAME_FILE_NUMBER)
        {
            if (
                !_saveDataStorage.TryLoad(
                    file_number,
                    PlayerStartPos,
                    GameConstants.SCENE_NAME_TUTORIAL_START,
                    out fileData,
                    out string errorMessage
                )
            )
            {
                Debug.LogError(errorMessage);
                AbortLoad(playerManagerOnLoadStart);
                yield break;
            }

            if (!TryApplySavedGame(fileData))
            {
                AbortLoad(playerManagerOnLoadStart);
                yield break;
            }
        }
        else
        {
            InitializeNewGameData();
            fileData = new SaveGameFileData
            {
                PlayerPosition = PlayerStartPos,
                SceneName = GameConstants.SCENE_NAME_TUTORIAL_START,
                PlayTime = 0f,
            };
        }

        //他のオブジェクトのStartメソッドでisFirstGameSceneOpenが必要なので、この位置で下記のことを行う
        if (!GameManager.isFirstGameSceneOpen)
        {
            GameManager.isFirstGameSceneOpen = true; //初回ゲームシーンオープンフラグを立てる
        }

        yield return LoadSceneAndPlayTime(fileData, loadScene);

        Vector3 playerPosition = CorrectPlayerLoadPosition(fileData.PlayerPosition);

        //シーンロードが完了したので、"新しいシーンの" PlayerManagerを改めて取得する
        PlayerManager playerManagerInNewScene = PlayerManager.instance;
        yield return RestorePlayerAfterLoad(playerManagerInNewScene, playerPosition, file_number);

        RestoreRuntimeState(file_number);

        // プレイヤーと敵が同時に出現した場合、即座に物理演算が再開すると
        // ロード直後にダメージを受ける/敵と接触する などの不具合が起こりうるため1フレームだけ待機
        yield return null;
        CompleteLoad(playerManagerInNewScene);
    }

    private PlayerManager BeginLoad()
    {
        isLoading = true;
        OnLoadingStateChanged?.Invoke(true);
        TimeManager.instance.RequestPause();

        PlayerManager playerManager = PlayerManager.instance;
        playerManager?.LockControl();
        FadeCanvas.instance.FadeOut(Mathf.Epsilon);
        BGMManager.instance?.Stop();
        SEManager.instance?.StopAllSE();
        return playerManager;
    }

    private bool TryApplySavedGame(SaveGameFileData fileData)
    {
        try
        {
            fileData.SaveData.Validate();
            SaveDataMigrationRunner.MigrateToCurrent(fileData.SaveData);
            fileData.SaveData.Validate();
        }
        catch (Exception ex)
        {
            Debug.LogError($"セーブデータのマイグレーションに失敗しました: {ex.Message}");
            return false;
        }

        GameManager.instance.savedata = fileData.SaveData;
        SkillManager.instance?.RebuildSkillCache();
        WeaponManager.instance?.ReplaceAllEquippedWeaponsWithInventoryReferences();
        ReplaceAllSlotItemWithInventoryReferences();

        if (fileData.HasFlagData)
        {
            FlagManager.instance.LoadFlagData(fileData.FlagData);
        }
        else
        {
            Debug.Log("FlagDataのセーブデータが存在しません。");
        }

        return true;
    }

    private void InitializeNewGameData()
    {
        if (GameManager.instance?.savedata?.WeaponInventoryData != null)
            GameManager.instance.savedata.WeaponInventoryData.AddWeapon(ShootName.Normal);
        else
            Debug.LogWarning("WeaponInventoryDataが存在しません");

        if (WeaponManager.instance != null)
            WeaponManager.instance.ReplaceEquippedWeapon(ShootName.Normal);
        else
            Debug.LogWarning("WeaponManagerが存在しません");

        if (GameManager.instance?.savedata?.FastTravelData != null)
        {
            GameManager.instance.savedata.FastTravelData.RegisterFastTravelData(
                FastTravelName.TutorialStage
            );
            GameManager.instance.savedata.FastTravelData.SetLastUsedFastTravel(
                FastTravelName.TutorialStage
            );
        }
        else
        {
            Debug.LogWarning("FastTravelDataが存在しません");
        }
    }

    private IEnumerator LoadSceneAndPlayTime(SaveGameFileData fileData, bool loadScene)
    {
        if (loadScene)
        {
            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(fileData.SceneName);
            SynchronizePlayTime(fileData.PlayTime);
            yield return new WaitUntil(() => asyncLoad.isDone);
        }
        else
        {
            SynchronizePlayTime(fileData.PlayTime);
        }
    }

    private void SynchronizePlayTime(float playTime)
    {
        StartTime = playTime;
        timeSinceLoad = Time.time;
    }

    private Vector3 CorrectPlayerLoadPosition(Vector3 playerPosition)
    {
        foreach (var corrector in PlayerSpawnCorrectorArea.ActiveInstances)
        {
            if (corrector == null || !corrector.IsPositionInArea(playerPosition))
                continue;

            return corrector.GetSafeSpawnPosition();
        }

        return playerPosition;
    }

    private IEnumerator RestorePlayerAfterLoad(
        PlayerManager playerManager,
        Vector3 playerPosition,
        int fileNumber
    )
    {
        if (playerManager == null)
        {
            Debug.LogError("シーンロード後にPlayerManagerが見つかりませんでした。");
            yield break;
        }

        yield return playerManager.StartCoroutine(playerManager.PlayerMove(playerPosition));

        if (fileNumber != GameConstants.NEW_GAME_FILE_NUMBER)
        {
            playerManager.EnableInvincibility(GameConstants.INVINCIBLE_DURATION_ON_LOAD);
        }
    }

    private void RestoreRuntimeState(int fileNumber)
    {
        if (fileNumber != GameConstants.NEW_GAME_FILE_NUMBER)
            FadeCanvas.instance.FadeIn(0.5f);

        if (WeaponManager.instance != null)
            WeaponManager.instance.RebuildOwnedWeaponData();
        else
            Debug.LogWarning("WeaponManagerが存在しません");

        ApplyAudioSettings();
    }

    private void CompleteLoad(PlayerManager playerManager)
    {
        TimeManager.instance.ReleasePause();
        GameManager.instance.EndTalk();
        EnableSave();
        isLoading = false;
        OnLoadingStateChanged?.Invoke(false);
        playerManager?.UnlockControl();
        _timeSinceLastSave = 0f;
    }

    private void AbortLoad(PlayerManager playerManager)
    {
        TimeManager.instance?.ReleasePause();
        playerManager?.UnlockControl();
        FadeCanvas.instance?.FadeIn(0.5f);
        isLoading = false;
        OnLoadingStateChanged?.Invoke(false);
    }

#if UNITY_EDITOR
    /// <summary>
    /// デバッグ実行時、他のManagerの初期化(Start)を待ってからロードを行うコルーチン
    /// </summary>
    private IEnumerator DebugLoadSequence(int slotNumber)
    {
        // 他のManager（特にBGMManager）のStartが走り切るのを1フレーム待つ
        yield return null;

        Debug.Log(
            $"<color=yellow>[Debug]</color> タイトル以外のシーンから開始されました。スロット {slotNumber} をロードして開始します（シーン遷移なし）。"
        );

        GameManager.instance?.ResetState();
        GameManager.isFirstGameOpen = true;
        SetToLoadMode();

        // シーン遷移なし(false)でロードを実行
        yield return StartCoroutine(SaveLoad(slotNumber, false));
    }
#endif

    /// <summary>
    /// 指定されたファイル番号にゲームデータをセーブする
    /// </summary>
    /// <param name="file_number">セーブするファイル番号</param>
    private void PerformSave(int file_number)
    {
        isOnSave = true; //一応セーブ待機中のフラグをON

        if (PlayerManager.instance == null)
        {
            Debug.LogError("PlayerManagerが見つかりません。セーブできません。");
            isOnSave = false; //フラグを戻す
            return;
        }

        // PlayerEffectManagerから最新のバフ情報を取得してsaveDataに反映
        if (PlayerEffectManager.instance != null)
        {
            // PlayerEffectManagerのリアルタイムデータを取得
            List<PlayerEffectStates> currentEffects =
                PlayerEffectManager.instance.GetCurrentEffectStatesForSave();

            // GameManagerのsavedata（PlayerStatus内）に上書き
            if (GameManager.instance.savedata.PlayerStatus != null)
            {
                GameManager.instance.savedata.PlayerStatus.playerEffectStates = currentEffects;
            }
            else
            {
                Debug.LogWarning(
                    "PlayerStatusDataがnullのため、エフェクト情報を保存できませんでした。"
                );
                isOnSave = false; //フラグを戻す
                return;
            }
        }
        else
        {
            Debug.LogWarning(
                "PlayerEffectManagerが見つからないため、エフェクト情報を保存できませんでした。"
            );
            isOnSave = false; //フラグを戻す
            return;
        }

        // ゲームのバージョンをsaveDataに取得
        GameManager.instance.savedata.GameVersion = Application.version;
        GameManager.instance.savedata.SaveSchemaVersion = SaveDataMigrationRunner.CurrentVersion;
        // 現在の日付・時刻をフォーマットして代入（例: "2026/02/26 17:30:00"）
        GameManager.instance.savedata.SaveDateTime = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");
        float newPlayTime = StartTime + Time.time - timeSinceLoad;
        int currentExperience = PlayerManager.instance.GetPlayerIntStatus(
            PlayerStatusIntName.playerExp
        );
        SaveGameFileData fileData = new SaveGameFileData
        {
            SaveData = GameManager.instance.savedata,
            FlagData = FlagManager.instance.SaveFlagData(),
            HasFlagData = true,
            PlayerPosition = PlayerManager.instance.GetPlayerPosition(),
            SceneName = SceneManager.GetActiveScene().name,
            PlayTime = newPlayTime,
            PlayerExperience = currentExperience,
        };
        if (!_saveDataStorage.TrySave(file_number, fileData, out string errorMessage))
        {
            Debug.LogError(errorMessage);
            isOnSave = false;
            return;
        }

        // --- メモリ上のスロット情報 (FileSlotInfos) の更新 ---
        // 辞書にキーが存在するか確認
        if (FileSlotInfos.ContainsKey(file_number))
        {
            // 既存の SaveSlotInfo オブジェクトの値を更新
            FileSlotInfos[file_number].playTime = newPlayTime;
            FileSlotInfos[file_number].experience = currentExperience;
        }
        else
        {
            // もしキーが存在しない場合（通常はAwakeで初期化されるはずだが念のため）、
            // 新しい SaveSlotInfo オブジェクトを作成して辞書に追加
            FileSlotInfos.Add(file_number, new SaveSlotInfo(newPlayTime, currentExperience));
            Debug.LogWarning(
                $"FileSlotInfosにキー {file_number} が存在しなかったため、新しく追加しました。"
            );
        }

        isOnSave = false; //セーブ待機中のフラグをOFF
    }

    /// <summary>
    /// 新規ゲームを開始する
    /// </summary>
    public void newLoad()
    {
        if (isLoading)
        {
            Debug.LogWarning("すでにロード中です。重複呼び出しを防止");
            return;
        }

        if (GameManager.instance != null)
        {
            GameManager.instance.ResetState(); //ゲーム内の変数を初期化
        }
        else
        {
            Debug.LogWarning("GameManagerが存在しません");
        }

        if (FlagManager.instance != null)
        {
            FlagManager.instance.ResetAllFlags(); //ゲーム内のフラグ変数を初期化
        }
        else
        {
            Debug.LogWarning("FlagManagerが存在しません");
        }

        SetToLoadMode(); //ロード状態にする
        StartCoroutine(SaveLoad(GameConstants.NEW_GAME_FILE_NUMBER)); //新規ゲームをロード
    }

    /// <summary>
    /// オートセーブを実行する
    /// </summary>
    // このメソッドはstaticにしません。
    // `isOnSave`や`IsLoading`といったインスタンスの状態（メンバー変数）に依存し、
    // インスタンスメソッドである`PerformSave`を呼び出す必要があるためです。
    // シングルトンであるこのクラスは、`SaveLoadManager.instance`を介してアクセスすることで、
    // 唯一のインスタンスの状態を正しく扱うことが意図されています。
    public void ExecuteAutoSave()
    {
        // オートセーブを実行中、またはロード中は処理しない
        if (isOnSave || IsLoading)
        {
            Debug.Log("セーブ/ロード中のため、オートセーブをスキップしました。");
            return;
        }

        Debug.Log("オートセーブを実行します。");
        // 定義したオートセーブ用のファイル番号でセーブ処理を呼び出す
        PerformSave(GameConstants.AUTO_SAVE_FILE_NUMBER);
        // オートセーブのタイマーをリセット
        _timeSinceLastSave = 0f;
    }

    /// <summary>
    /// 設定された時間を超えていればオートセーブを実行する
    /// </summary>
    public void AutoSaveByTime()
    {
        if (_timeSinceLastSave >= GameConstants.AUTO_SAVE_INTERVAL)
        {
            ExecuteAutoSave();
        }
    }

    /// セーブを有効にするメソッド
    /// <summary>
    public void EnableSave()
    {
        isEnableSave = true;
        OnEnableSaveStateChanged?.Invoke(isEnableSave); // セーブ可能状態が変化したことを通知
    }

    /// セーブを無効にするメソッド
    /// <summary>
    public void DisableSave()
    {
        isEnableSave = false;
        OnEnableSaveStateChanged?.Invoke(isEnableSave); // セーブ可能状態が変化したことを通知
    }

    /// <summary>
    /// 次のSaveLoad処理を「セーブモード」に設定する
    /// </summary>
    public void SetToSaveMode()
    {
        CurrentSaveLoadMode = SaveLoadMode.Save;
    }

    /// <summary>
    /// 次のSaveLoad処理を「ロードモード」に設定する
    /// </summary>
    public void SetToLoadMode()
    {
        CurrentSaveLoadMode = SaveLoadMode.Load;
    }

    /// <summary>
    /// スロット中の全てのアイテムを、所持アイテムの参照に置き換える
    /// このメソッドは、QuickItemDataのアイテムを
    /// 所持アイテムの参照に置き換えるために使用されます。
    /// /// 例えば、QuickItemDataのアイテムが
    /// ItemEntry(1, 0) の場合、所持アイテムのリストから
    /// itemIDが1のアイテムを探し、
    /// その参照に置き換えます。
    /// もし所持アイテムに存在しない場合は、ダミーの空アイテムを追加します。
    /// /// なお、QuickItemDataのアイテムがnullの場合は、
    /// そのままnullを追加します。
    /// </summary>
    public void ReplaceAllSlotItemWithInventoryReferences()
    {
        var sourceList = GameManager.instance?.savedata?.ItemInventoryData.ownedItems;
        var quickList = GameManager.instance?.savedata?.QuickItemData.ownedItems;

        if (sourceList == null || quickList == null)
        {
            Debug.LogError("ItemInventoryDataまたはQuickItemDataがnullです");
            return;
        }

        // スロット中の全てのitemIDをリスト化
        // nullを含む可能性があるため、nullチェックしながらIDを収集
        List<int?> quickListIDs = quickList.Select(q => q?.itemID).ToList();

        // クリア（nullも含めて再構成する）
        quickList.Clear();

        foreach (int? itemID in quickListIDs)
        {
            if (!itemID.HasValue)
            {
                quickList.Add(null); // 元がnullだった場合もnullを追加
                continue;
            }

            var inventoryItem = sourceList.Find(q => q.itemID == itemID.Value);
            if (inventoryItem != null)
            {
                quickList.Add(inventoryItem); // 参照に置き換え
            }
            else
            {
                quickList.Add(new ItemEntry(itemID.Value, 0)); // ダミーの空アイテムで補完
            }
        }
    }

    // === 設定データのセーブ・ロード ===

    public void LoadSettings()
    {
        _settingsStorage.LoadSettings();
    }

    public void SaveSettings()
    {
        _settingsStorage.SaveSettings();
    }

    /// <summary>
    /// セーブスロットに依存しないデバッグ設定を読み込みます。
    /// </summary>
    public void LoadDebugSettings()
    {
        _settingsStorage.LoadDebugSettings();
    }

    /// <summary>
    /// セーブスロットに依存しないデバッグ設定を保存します。
    /// </summary>
    public void SaveDebugSettings()
    {
        _settingsStorage.SaveDebugSettings();
    }

    /// <summary>
    /// SettingsファイルからBGMとSEの音量設定を読み込み、各Managerに適用します。
    /// </summary>
    public void ApplyAudioSettings()
    {
        if (Settings == null)
        {
            Debug.LogWarning("SaveLoadManagerのSettingsがnullです。音量設定を適用できません。");
            return; // Settingsがなければ何もできない
        }

        // BGM音量の適用
        if (BGMManager.instance == null)
        {
            Debug.LogWarning("BGMManagerが存在しません");
        }
        else
        {
            BGMManager.instance.AdjustAllVolume(Settings.bgmVolume); //BGM音量を設定
        }

        // SE音量の適用
        if (SEManager.instance == null)
        {
            Debug.LogWarning("SEManagerが存在しません");
        }
        else
        {
            SEManager.instance.AdjustAllSEVolume(Settings.seVolume); //SE音量を設定
        }
    }
}
