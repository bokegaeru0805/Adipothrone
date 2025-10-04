using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SaveLoadManager : MonoBehaviour
{
    public static SaveLoadManager instance { get; private set; } //シングルトンインスタンス

    // --- ファイルパスとキーの定義 ---
    private const string SETTINGS_FILE_PATH = "GameSettings.es3";
    private Vector2 PlayerStartPos = new Vector2(-110, 0); //プレイヤーの初期座標

    // --- 現在ロードしているデータ ---
    public GameSettingsSaveData Settings { get; private set; }
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

    //セーブデータのプレイ時間を保存する辞書
    public static Dictionary<int, float> FilePlaytime;

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

            string currentGameVersion = Application.version; //現在のゲームのバージョンを取得
            FilePlaytime = new Dictionary<int, float>(); //ゲームのプレイ時間を保存する変数を初期化
            isOnSave = false; //セーブ待機中のフラグを初期化

            if (FilePlaytime == null)
            {
                Debug.LogWarning("FilePlaytimeが初期化されていません。");
                return;
            }
            else
            {
                for (
                    int i = GameConstants.AUTO_SAVE_FILE_NUMBER;
                    i < GameConstants.MaxSaveLoadFiles + GameConstants.MAX_AUTOSAVE_FOLDERS;
                    i++
                )
                {
                    //FilePlayTimeにファイルごとのプレイ時間のデータを保存。もしデータがない場合は0を保存。
                    ES3Settings settings = new ES3Settings(GetSaveFilePath(i));
                    FilePlaytime.Add(i, ES3.Load<float>("PlayTime", defaultValue: 0, settings));

                    //データがない場合は飛ばす
                    if (SaveLoadManager.FilePlaytime[i] == 0)
                        continue;

                    //セーブデータのゲームバージョンを取得
                    string dataGameVersion;
                    try
                    {
                        // セーブデータを読み込む（存在しない、破損などの場合は例外が出る可能性あり）
                        var loadedData = ES3.Load<SaveData>("SaveData", settings);

                        // 読み込んだデータからバージョン情報を取得（null 安全アクセス）
                        dataGameVersion = loadedData?.GameVersion;

                        // バージョン情報が null または空文字の場合は不正とみなしてスキップ
                        if (string.IsNullOrEmpty(dataGameVersion))
                        {
                            Debug.LogWarning(
                                $"セーブデータにバージョン情報が存在しません（スロット {i}）"
                            );
                            FilePlaytime[i] = 0;
                            continue;
                        }
                    }
                    catch (Exception ex)
                    {
                        // 読み込み時に何らかの例外が発生した場合は、ファイルをスキップしプレイ時間を0に
                        Debug.LogError(
                            $"セーブデータの読み込みに失敗（スロット {i}）: {ex.Message}"
                        );
                        FilePlaytime[i] = 0;
                        continue;
                    }

                    // 保存されたゲームバージョンと現在のバージョンが異なる場合、プレイ時間を無効化してファイルを非表示化
                    if (dataGameVersion != currentGameVersion)
                    {
                        FilePlaytime[i] = 0;
                    }
                }
            }
    }

    private void Start()
    {
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

    public IEnumerator SaveLoad(int file_number)
    {
        if (CurrentSaveLoadMode == SaveLoadMode.Save)
        {
            PerformSave(file_number);
            yield break; //セーブ処理は同期的なのでここでコルーチンを終了
        }
        else if (CurrentSaveLoadMode == SaveLoadMode.Load)
        {
            // ロード中のフラグを立て、イベントを発行
            isLoading = true;
            OnLoadingStateChanged?.Invoke(true);
            //一応時間停止
            TimeManager.instance.RequestPause();
            // ロード開始時点でのPlayerManager（もし存在すれば）の操作をロック
            var playerManagerOnLoadStart = PlayerManager.instance;
            playerManagerOnLoadStart?.LockControl();

            //画面を即座に暗転させる
            FadeCanvas.instance.FadeOut(Mathf.Epsilon);
            //BGMを全て停止
            BGMManager.instance?.Stop();
            //SEを全て停止
            SEManager.instance?.StopAllSE();
            //ファイルパスを生成
            string filePath = GetSaveFilePath(file_number);

            if (file_number != GameConstants.NEW_GAME_FILE_NUMBER)
            {
                if (ES3.KeyExists("SaveData", filePath))
                {
                    //セーブデータをロード
                    SaveData saveData = ES3.Load<SaveData>("SaveData", filePath);
                    //セーブデータをGameManagerに保存
                    GameManager.instance.savedata = saveData;

                    //装備中の全武器のIDを取得し、同じIDの所持武器(inventory)の参照に置き換える
                    WeaponManager.instance?.ReplaceAllEquippedWeaponsWithInventoryReferences();

                    //スロット中の全アイテムのIDを取得し、同じIDの所持アイテム(inventory)の参照に置き換える
                    ReplaceAllSlotItemWithInventoryReferences();

                    // SaveData の null-safe 初期化
                    if (GameManager.instance?.savedata == null)
                    {
                        GameManager.instance.savedata = new SaveData();
                    }

                    // プレイヤーステータス
                    if (GameManager.instance?.savedata?.PlayerStatus == null)
                    {
                        GameManager.instance.savedata.PlayerStatus = new PlayerStatusData();
                    }

                    // 宝箱データ
                    if (GameManager.instance?.savedata?.TreasureData == null)
                    {
                        GameManager.instance.savedata.TreasureData = new TreasureData();
                    }

                    // // クエスト進行度
                    // if (GameManager.instance?.savedata?.questData == null)
                    // {
                    //     GameManager.instance.savedata.questData = new QuestData();
                    // }

                    // 所持アイテム
                    if (GameManager.instance?.savedata?.ItemInventoryData == null)
                    {
                        GameManager.instance.savedata.ItemInventoryData = new InventoryItemData();
                    }

                    // 所持武器
                    if (GameManager.instance?.savedata?.WeaponInventoryData == null)
                    {
                        GameManager.instance.savedata.WeaponInventoryData =
                            new InventoryWeaponData();
                    }

                    // 装備武器
                    if (GameManager.instance?.savedata?.WeaponEquipmentData == null)
                    {
                        GameManager.instance.savedata.WeaponEquipmentData =
                            new InventoryWeaponData();
                    }

                    // ファストトラベルデータ
                    if (GameManager.instance?.savedata?.FastTravelData == null)
                    {
                        GameManager.instance.savedata.FastTravelData = new FastTravelData();
                    }
                }
                else
                {
                    Debug.LogError("SaveDataのセーブデータが存在しません。");
                    yield break;
                }

                // フラグデータをロード
                if (ES3.KeyExists("FlagSaveKey", filePath))
                {
                    FlagManager.FlagSaveData flagData = ES3.Load<FlagManager.FlagSaveData>(
                        "FlagSaveKey",
                        filePath
                    );
                    FlagManager.instance.LoadFlagData(flagData);
                }
                else
                {
                    Debug.Log("FlagDataのセーブデータが存在しません。");
                }
            }
            else
            {
                if (GameManager.instance?.savedata?.WeaponInventoryData != null)
                {
                    GameManager.instance.savedata.WeaponInventoryData.AddWeapon(ShootName.normal); //初期shoot
                }
                else
                {
                    Debug.LogWarning("WeaponInventoryDataが存在しません");
                }

                if (WeaponManager.instance != null)
                {
                    WeaponManager.instance.ReplaceEquippedWeapon(ShootName.normal); //初期shootを装備に追加
                }
                else
                {
                    Debug.LogWarning("WeaponInventoryDataが存在しません");
                }

                if (GameManager.instance?.savedata?.FastTravelData != null)
                {
                    GameManager.instance.savedata.FastTravelData.RegisterFastTravelData(
                        FastTravelName.TutorialStage
                    ); //チュートリアルステージのファストトラベルを登録
                    GameManager.instance.savedata.FastTravelData.SetLastUsedFastTravel(
                        FastTravelName.TutorialStage
                    ); //チュートリアルステージのファストトラベルを最後に使用した地点として設定
                }
                else
                {
                    Debug.LogWarning("FastTravelDataが存在しません");
                }
            }

            //他のオブジェクトのStartメソッドでisFirstGameSceneOpenが必要なので、この位置で下記のことを行う
            if (!GameManager.isFirstGameSceneOpen)
            {
                GameManager.isFirstGameSceneOpen = true; //初回ゲームシーンオープンフラグを立てる
            }

            string sceneName = GameConstants.SceneName_TutorialStart; //デフォルトのシーン名を設定

            // セーブデータからシーン名を読み込む（存在チェックも含める）
            if (ES3.KeyExists("CurrentSceneName", filePath))
            {
                sceneName = ES3.Load<string>("CurrentSceneName", filePath);
            }

            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName); //Sceneをロード

            //セーブデータのプレイ時間を更新
            if (
                file_number != GameConstants.NEW_GAME_FILE_NUMBER
                && ES3.KeyExists("PlayTime", GetSaveFilePath(file_number))
            )
            {
                SaveLoadManager.StartTime = ES3.Load<float>("PlayTime", filePath);
            }
            else
            {
                SaveLoadManager.StartTime = 0f; //開始時間を初期化
            }
            //ロードしてからの時間を更新
            SaveLoadManager.timeSinceLoad = Time.time;

            //プレイヤーの初期座標を初期化
            Vector3 PlayerPosition = new Vector2();
            if (
                //プレイヤーの初期座標のセーブデータが存在する場合
                file_number != GameConstants.NEW_GAME_FILE_NUMBER
                && ES3.KeyExists("PlayerPosition", filePath)
            )
            {
                //プレイヤーの初期座標を適用
                PlayerPosition = ES3.Load<Vector2>("PlayerPosition", filePath);
            }
            else
            {
                //プレイヤーの初期座標がセーブされていない場合は、GameManagerのPlayerStartPosを使用
                PlayerPosition = PlayerStartPos;
            }

            //シーンが読み込み完了するまで待つ
            yield return new WaitUntil(() => asyncLoad.isDone);

            //シーンロードが完了したので、"新しいシーンの" PlayerManagerを改めて取得する
            var playerManagerInNewScene = PlayerManager.instance;

            // 取得したインスタンスを使い回し、nullチェックを1回にまとめる
            if (playerManagerInNewScene != null)
            {
                // プレイヤーの初期座標を移動させ、同時にカメラの追従完了を待つ
                // PlayerMoveがコルーチンを返すので、yield return で待機する
                yield return playerManagerInNewScene.StartCoroutine(
                    playerManagerInNewScene.PlayerMove(PlayerPosition)
                );

                // プレイヤーを一定時間無敵化
                if (file_number != GameConstants.NEW_GAME_FILE_NUMBER)
                {
                    playerManagerInNewScene.EnableInvincibility(5);
                }
            }
            else
            {
                Debug.LogError("シーンロード後にPlayerManagerが見つかりませんでした。");
            }

            if (file_number != GameConstants.NEW_GAME_FILE_NUMBER)
            {
                FadeCanvas.instance.FadeIn(0.5f); //画面を明転させる
            }

            if (WeaponManager.instance != null)
            {
                //セーブデータからの参照用辞書・リストの再構築
                WeaponManager.instance.RebuildOwnedWeaponData();
            }
            else
            {
                Debug.LogWarning("WeaponManagerが存在しません");
            }

            // プレイヤーと敵が同時に出現した場合、即座に物理演算が再開すると
            // ロード直後にダメージを受ける/敵と接触する などの不具合が起こりうるため
            yield return null; // 1フレームだけ待機（十分なケースが多い）
            TimeManager.instance.ReleasePause(); // 時間の進行を再開
            //会話が発生するようにする
            GameManager.instance.EndTalk(); // 会話中フラグをOFFにする
            //セーブをできるようにする
            EnableSave();
            //ロード完了後、フラグを下げてイベントを発行
            isLoading = false;
            OnLoadingStateChanged?.Invoke(false);
            // 再び移動を許可
            playerManagerInNewScene.UnlockControl(); // ロード開始時点でのPlayerManagerの操作を解除
            //オートセーブのタイマーをリセット
            _timeSinceLastSave = 0f;
        }
    }

    /// <summary>
    /// 指定されたファイル番号にゲームデータをセーブする
    /// </summary>
    /// <param name="file_number">セーブするファイル番号</param>
    private void PerformSave(int file_number)
    {
        isOnSave = true; //一応セーブ待機中のフラグをON

        string filePath = GetSaveFilePath(file_number); //セーブファイルのパスを生成

        if (PlayerManager.instance == null)
        {
            Debug.LogError("PlayerManagerが見つかりません。セーブできません。");
            isOnSave = false; //フラグを戻す
            return;
        }

        // ゲームのバージョンをsaveDataに取得
        GameManager.instance.savedata.GameVersion = Application.version;
        //セーブデータを取得
        SaveData saveData = GameManager.instance.savedata;
        //セーブデータを保存
        ES3.Save("SaveData", saveData, filePath);

        //フラグデータを取得
        FlagManager.FlagSaveData flagData = FlagManager.instance.SaveFlagData();
        //フラグデータを別途保存
        ES3.Save("FlagSaveKey", flagData, filePath);

        //プレイヤーの座標を取得
        Vector2 playerPos = PlayerManager.instance.GetPlayerPosition();
        //Playerの座標を保存
        ES3.Save<Vector2>("PlayerPosition", playerPos, filePath);

        // 現在のシーン名を取得
        string currentSceneName = SceneManager.GetActiveScene().name;

        // シーン名をセーブデータに保存
        ES3.Save<string>("CurrentSceneName", currentSceneName, filePath);

        //プレイ時間として、元々のデータのプレイ時間にロードしてからのプレイ時間を加えて保存
        float newPlayTime = StartTime + Time.time - timeSinceLoad;
        ES3.Save<float>("PlayTime", newPlayTime, filePath);

        // メモリ上のプレイ時間データも更新
        if (FilePlaytime.ContainsKey(file_number))
        {
            FilePlaytime[file_number] = newPlayTime;
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
    /// 指定されたファイル番号に対応するセーブファイルのパスを取得する
    /// </summary>
    /// <param name="fileNumber">セーブファイルの番号</param>
    private string GetSaveFilePath(int fileNumber)
    {
        return $"Adipothrone_File{fileNumber}.es3";
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
        Settings = ES3.Load<GameSettingsSaveData>(
            "settings",
            SETTINGS_FILE_PATH,
            new GameSettingsSaveData()
        );
    }

    public void SaveSettings()
    {
        ES3.Save<GameSettingsSaveData>("settings", Settings, SETTINGS_FILE_PATH);
    }
}
