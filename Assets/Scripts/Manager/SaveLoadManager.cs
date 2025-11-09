using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 各セーブスロットの概要情報（プレイ時間と経験値）を格納するクラス
/// </summary>
[System.Serializable] // InspectorやES3で扱えるようにする
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

        Debug.Log($"[{manager.GetType().Name}] がアクティブManagerとしてスタックに登録されました。");
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
                $"Managerスタックの登録解除エラー: " + 
                $"解除しようとした [{manager.GetType().Name}] はスタックの一番上にいません。",
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
                Debug.LogWarning($"[{manager.GetType().Name}] をスタックの途中から強制的に削除しました。");
            }
            return;
        }

        // 正常な解除処理
        var poppedManager = managerStack.Pop();
        Debug.Log($"[{poppedManager.GetType().Name}] がスタックから登録解除されました。");

        if (CurrentActiveManager != null)
        {
            Debug.Log($"アクティブManagerは [{CurrentActiveManager.GetType().Name}] に戻りました。");
        }
        else
        {
            Debug.Log("アクティブなManagerスタックは空になりました。");
        }
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

        Version currentGameVersion = new Version(Application.version); // 現在のゲームバージョンをVersionオブジェクトとして取得
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
                // 各セーブファイル用のES3設定を作成
                ES3Settings settings = new ES3Settings(GetSaveFilePath(i));

                float loadedPlayTime = 0f; // ロードしたプレイ時間（デフォルト0）
                int loadedExperience = 0; // ロードした経験値（デフォルト0）
                string dataGameVersionStr = null; // セーブデータのバージョン文字列（デフォルトnull）

                // まず、セーブファイル自体が存在するか確認
                if (ES3.FileExists(GetSaveFilePath(i)))
                {
                    // プレイ時間をロード (これはトップレベルにある)
                    // defaultValue: 0f を指定することで、キーが存在しない場合でもエラーにならず0fが返る
                    loadedPlayTime = ES3.Load<float>("PlayTime", defaultValue: 0f, settings);

                    // プレイ時間が0（＝有効なセーブデータではない）場合は、経験値などをロードせずに次のスロットへ
                    if (loadedPlayTime == 0f)
                    {
                        // プレイ時間も経験値も0のSaveSlotInfoを追加して次へ
                        FileSlotInfos.Add(i, new SaveSlotInfo(loadedPlayTime, loadedExperience));
                        continue;
                    }

                    // SaveDataオブジェクトが存在するか確認してからロード
                    if (ES3.KeyExists("SaveData", settings))
                    {
                        try
                        {
                            // SaveDataオブジェクト全体を読み込む（バージョン情報やプレイヤー情報が含まれる）
                            var loadedSaveData = ES3.Load<SaveData>("SaveData", settings);

                            // 読み込んだデータからバージョン情報を取得（null安全アクセス ?. を使用）
                            dataGameVersionStr = loadedSaveData?.GameVersion;

                            // 読み込んだデータから経験値を取得（PlayerStatusがnullでないことも確認）
                            if (loadedSaveData != null && loadedSaveData.PlayerStatus != null)
                            {
                                loadedExperience = loadedSaveData.PlayerStatus.playerExp;
                            }
                            else
                            {
                                // SaveDataまたはPlayerStatusが不正な場合は警告を出し、データを無効化
                                Debug.LogWarning(
                                    $"スロット {i} の SaveData または PlayerStatus が不正です。データを無効として扱います。"
                                );
                                loadedPlayTime = 0f; // プレイ時間もリセット
                                loadedExperience = 0; // 経験値もリセット
                            }
                        }
                        catch (Exception ex)
                        {
                            // 読み込み時に何らかの例外が発生した場合（データ破損など）
                            Debug.LogError(
                                $"SaveDataの読み込みに失敗（スロット {i}）: {ex.Message}"
                            );
                            // データを無効として扱う
                            loadedPlayTime = 0f;
                            loadedExperience = 0;
                        }
                    }
                    else
                    {
                        // PlayTimeはあるがSaveDataがない、という異常な状態
                        Debug.LogWarning(
                            $"スロット {i} に SaveData キーが存在しません。データを無効として扱います。"
                        );
                        // データを無効として扱う
                        loadedPlayTime = 0f;
                        loadedExperience = 0;
                    }

                    // --- バージョンチェック ---
                    // この時点で loadedPlayTime > 0f であり、かつ dataGameVersionStr が取得できている場合のみ実行
                    if (loadedPlayTime > 0f && !string.IsNullOrEmpty(dataGameVersionStr))
                    {
                        try
                        {
                            Version dataGameVersion = new Version(dataGameVersionStr);

                            // セーブデータのバージョンが現在のゲームバージョンより新しい場合、
                            // 未来のバージョンなので互換性がないとみなし、ロード対象から外す
                            if (dataGameVersion > currentGameVersion) // currentGameVersionはAwakeの冒頭で取得済みと仮定
                            {
                                Debug.LogWarning(
                                    $"スロット {i} のセーブデータは新しいバージョンのためロードできません。(データ: {dataGameVersion}, ゲーム: {currentGameVersion})"
                                );
                                // データを無効として扱う
                                loadedPlayTime = 0f;
                                loadedExperience = 0;
                            }
                        }
                        catch (FormatException ex) // Version文字列のフォーマットが不正な場合
                        {
                            Debug.LogError(
                                $"スロット {i} のバージョン文字列 '{dataGameVersionStr}' の形式が不正です: {ex.Message}"
                            );
                            // データを無効として扱う
                            loadedPlayTime = 0f;
                            loadedExperience = 0;
                        }
                        catch (Exception ex) // その他の予期せぬエラー
                        {
                            Debug.LogError(
                                $"バージョンチェック中に予期せぬエラーが発生しました（スロット {i}）: {ex.Message}"
                            );
                            // データを無効として扱う
                            loadedPlayTime = 0f;
                            loadedExperience = 0;
                        }
                    }
                    else if (loadedPlayTime > 0f && string.IsNullOrEmpty(dataGameVersionStr))
                    {
                        // SaveDataのロードに成功したがバージョン文字列が空だった場合（通常はtry内で処理されるはずだが念のため）
                        Debug.LogWarning(
                            $"セーブデータにバージョン情報が存在しません（スロット {i}）。データを無効として扱います。"
                        );
                        // データを無効として扱う
                        loadedPlayTime = 0f;
                        loadedExperience = 0;
                    }
                }

                // --- 最終的な値を辞書に追加 ---
                // ループの最後に、取得した（またはエラーでリセットされた）値でSaveSlotInfoを作成し、辞書に追加
                FileSlotInfos.Add(i, new SaveSlotInfo(loadedPlayTime, loadedExperience));
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

                    // マイグレーション処理の呼び出し
                    CheckAndMigrateSaveData(saveData);

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

#if DEMO_BUILD
            string sceneName = GameConstants.SceneName_Chapter1; //デモ版の場合、デフォルトのシーン名を変更
#else
            string sceneName = GameConstants.SceneName_TutorialStart; //デフォルトのシーン名を設定
#endif

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

#if DEMO_BUILD
                PlayerPosition = new Vector3(-200, 0, 0);
#else
                PlayerPosition = PlayerStartPos;
#endif
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

#if DEMO_BUILD
            FadeCanvas.instance.FadeIn(0.5f); //画面を明転させる
#endif

            if (WeaponManager.instance != null)
            {
                //セーブデータからの参照用辞書・リストの再構築
                WeaponManager.instance.RebuildOwnedWeaponData();
            }
            else
            {
                Debug.LogWarning("WeaponManagerが存在しません");
            }

            //BGMとSEの音量を適用
            //シーンが変わるCRIWAREの仕様により、カテゴリの音量がリセットされてしまう
            //そのため、再度音量を適用する必要がある
            ApplyAudioSettings();

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

        // プレイ時間として、元々のデータのプレイ時間にロードしてからのプレイ時間を加えて保存
        float newPlayTime = StartTime + Time.time - timeSinceLoad;
        ES3.Save<float>("PlayTime", newPlayTime, filePath);

        // --- PlayerEXPの保存 ---
        // 現在のプレイヤー経験値を取得
        int currentExperience = PlayerManager.instance.GetPlayerIntStatus(
            PlayerStatusIntName.playerExp
        );
        ES3.Save<int>("PlayerEXP", currentExperience, filePath);

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
    /// 指定されたファイル番号に対応するセーブファイルのパスを取得する
    /// 決して変更しないでください。セーブ・ロードの整合性に関わります。
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
        // ロードする前に、設定ファイルがすでに存在するかどうかを確認
        bool settingsExist = ES3.KeyExists("settings", SETTINGS_FILE_PATH);

        // 従来通りロード処理を実行
        // (ファイルが存在しない場合は、ここで new GameSettingsSaveData() が生成される)
        Settings = ES3.Load<GameSettingsSaveData>(
            "settings",
            SETTINGS_FILE_PATH,
            new GameSettingsSaveData()
        );

        // もしファイルが存在しなかった場合（＝新しく生成された場合）
        if (!settingsExist)
        {
            // デバッグログを出力
            Debug.Log(
                "設定ファイルが見つからなかったため、新しい設定ファイルを生成し、保存しました。"
            );

            // 生成したばかりの設定をすぐに保存する
            SaveSettings();
        }
    }

    public void SaveSettings()
    {
        ES3.Save<GameSettingsSaveData>("settings", Settings, SETTINGS_FILE_PATH);
    }

    /// <summary>
    /// セーブデータのバージョンをチェックし、必要に応じて移行処理を実行する
    /// </summary>
    /// <param name="saveData">ロードしたセーブデータ</param>
    private void CheckAndMigrateSaveData(SaveData saveData)
    {
        // セーブデータにバージョン情報がない（＝最古バージョン）もしくは1.00の場合の初期値を設定
        if (string.IsNullOrEmpty(saveData.GameVersion) || saveData.GameVersion == "1.0")
        {
            saveData.GameVersion = "1.0.0"; // プロジェクトに応じた最古バージョンを指定
            Debug.Log("セーブデータにバージョン情報がなかったため、1.0.0を設定しました。");
        }

        try
        {
            Version currentGameVersion = new Version(Application.version);
            Version loadedDataVersion = new Version(saveData.GameVersion);

            if (loadedDataVersion < currentGameVersion)
            {
                Debug.Log(
                    $"古いセーブデータ（Ver: {loadedDataVersion}）を検出。マイグレーションを開始します。"
                );

                // --- 段階的マイグレーション ---
                // 新しいバージョンへの移行処理をここに追加していく

                // 例: 1.1.0 より前のバージョンから、1.1.0 へのアップデート
                if (loadedDataVersion < new Version("1.1.0"))
                {
                    MigrateToV1_1_0(saveData);
                }

                // 例: 将来 1.2.0 がリリースされた場合
                // if (loadedDataVersion < new Version("1.2.0"))
                // {
                //     MigrateToV1_2_0(saveData);
                // }


                // 全ての移行処理後、セーブデータ内のバージョンを最新に更新
                saveData.GameVersion = Application.version;
                Debug.Log("マイグレーションが完了しました。");
            }
        }
        catch (Exception e)
        {
            Debug.LogError(
                $"セーブデータのバージョンチェック、またはマイグレーションに失敗しました: {e.Message}"
            );
            // 必要に応じて、ロードを中止する、ユーザーに通知するなどの処理をここに記述
        }
    }

    /// <summary>
    /// ver 1.1.0 への具体的な移行処理
    /// </summary>
    private void MigrateToV1_1_0(SaveData saveData)
    {
        Debug.Log("バージョン 1.1.0 への更新処理を実行中...");

        // ここに具体的なデータ構造の変更処理を記述します。
        // 例：新しいTips機能が追加された場合
        // if (saveData.TipsData == null)
        // {
        //     saveData.TipsData = new TipsData();
        // }

        // 例：特定のフラグが立っていたら、新しいTipsを解放する
        // bool hasClearedTutorial = FlagManager.instance.GetFlag("TUTORIAL_CLEARED"); // ※このタイミングではFlagManagerはまだロードされていない可能性があるので注意
        // if (saveData.SomeOldFlag && !saveData.TipsData.unlockedTips.Contains(1))
        // {
        //      saveData.TipsData.unlockedTips.Add(1);
        // }
    }

    // 将来のバージョンアップ用
    // private void MigrateToV1_2_0(SaveData saveData)
    // {
    //     Debug.Log("バージョン 1.2.0 への更新処理を実行中...");
    // }

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
