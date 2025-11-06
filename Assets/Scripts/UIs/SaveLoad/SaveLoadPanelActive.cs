using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class SaveLoadPanelActive : MonoBehaviour
{
    [SerializeField]
    private PanelName panelName;

    [Header("セーブデータの選択ボタン (リスト)")]
    [SerializeField]
    private List<GameObject> fileSlotObjects = new List<GameObject>();
    private int currentTopFileNumber = 1; // 現在表示している一番上のファイル番号
    private InputManager inputManager;

    //1フレーム前の選択状態を記憶する変数
    private GameObject previousSelected = null;

    // モードによって変わるファイルリストの範囲を保持する変数
    private int _startFileNumber;
    private int _maxFileNumber;

    int pageSize = 3; // 画面に表示されているスロット数

    /// <summary>
    /// ファイルスロット1つ分のUIコンポーネントをまとめた内部クラス
    /// </summary>
    public class FileSlotUI
    {
        public GameObject slotObject;
        public SaveLoadFileButton button;
        public TextMeshProUGUI playTimeText;
        public TextMeshProUGUI levelText; // レベル表示（オプション）

        /// <summary>
        /// GameObjectから関連コンポーネントをキャッシュするコンストラクタ
        /// </summary>
        public FileSlotUI(GameObject obj)
        {
            slotObject = obj;

            if (obj == null)
            {
                Debug.LogError("FileSlotUIにnullのGameObjectが渡されました。");
                return;
            }

            // 1. ボタンコンポーネントを取得
            button = obj.GetComponent<SaveLoadFileButton>();
            if (button == null)
                Debug.LogError($"{obj.name} に SaveLoadFileButton がありません。");

            // 2. プレイ時間テキスト（子オブジェクト0）
            if (obj.transform.childCount > 0)
            {
                playTimeText = obj.transform.GetChild(0)?.GetComponent<TextMeshProUGUI>();
                if (playTimeText == null)
                    Debug.LogWarning(
                        $"{obj.name} の子オブジェクト0に TextMeshProUGUI がありません。"
                    );
            }
            else
            {
                Debug.LogWarning($"{obj.name} に子オブジェクトが存在しません。");
            }

            // 3. レベルテキスト（子オブジェクト1）
            if (obj.transform.childCount > 1)
            {
                levelText = obj.transform.GetChild(1)?.GetComponent<TextMeshProUGUI>();
                // レベルテキストはオプション扱い（nullでも警告しない）
            }
        }
    }

    // キャッシュされたUIスロットのリスト
    private List<FileSlotUI> fileSlots = new List<FileSlotUI>();

    private void Awake()
    {
        if (panelName == PanelName.None)
        {
            Debug.LogWarning($"{this.gameObject.name}のパネルの名前が設定されていません");
            return;
        }

        if (fileSlotObjects.Count == 0 || fileSlotObjects.Any(obj => obj == null))
        {
            Debug.LogWarning(
                $"{this.gameObject.name}のセーブデータの選択ボタン(fileSlotObjects)が設定されていないか、リスト内にnullの要素があります。"
            );
            return;
        }
        else
        {
            // 既存のリストをクリア
            fileSlots.Clear();

            // インスペクターで設定されたGameObjectリストから、
            // FileSlotUIクラスのインスタンスを作成してリストに追加する
            foreach (var fileObject in fileSlotObjects)
            {
                // コンストラクタ内でGetComponentとキャッシュが行われる
                fileSlots.Add(new FileSlotUI(fileObject));
            }

            pageSize = fileSlots.Count;

            // 起動時に必須コンポーネントが欠けていないか最終チェック
            if (fileSlots.Any(slot => slot.button == null || slot.playTimeText == null))
            {
                Debug.LogError(
                    $"{this.gameObject.name}のAwake処理でキャッシュに失敗しました。必須コンポーネント（ButtonまたはPlayTimeText）が不足しています。"
                );
            }
        }
    }

    private void Start()
    {
        // InputManagerのインスタンスを取得
        inputManager = InputManager.instance;
        if (inputManager == null)
        {
            Debug.LogError("InputManagerが見つかりません。");
            this.enabled = false; // スクリプトを無効化
        }
    }

    private enum PanelName
    {
        None = 0, // パネルが無効な状態
        Save = 10,
        Load = 20,
    }

    private void OnEnable() // パネルを SetActive(true) した直後に呼ばれる
    {
        //パネルが開かれたときに前回の選択状態をリセット
        previousSelected = null;

        if (panelName == PanelName.Save || panelName == PanelName.Load)
        {
            //  モードに応じてファイルリストの範囲を設定
            if (panelName == PanelName.Save)
            {
                SaveLoadManager.instance.SetToSaveMode(); //セーブ状態にする
                _startFileNumber = 1; // セーブ時はFile 1から
                _maxFileNumber = GameConstants.MaxSaveLoadFiles;
            }
            else if (panelName == PanelName.Load)
            {
                SaveLoadManager.instance.SetToLoadMode(); //ロード状態にする
                _startFileNumber = GameConstants.AUTO_SAVE_FILE_NUMBER; // ロード時はオートセーブ(0)から
                _maxFileNumber = GameConstants.MaxSaveLoadFiles; // File 0 ~ Max なので最大インデックスはMax
            }
            else
            {
                Debug.LogWarning($"{this.gameObject.name}のパネルの名前が正しく設定されていません");
                return;
            }

            //セーブ中かどうかのフラグをOFF
            SaveLoadManager.isOnSave = false;
            //セーブデータ確認画面を表示するフラグをOFF
            SaveLoadManager.isDataPrompting = false;

            //最後に選択したファイル番号から表示を開始する
            int lastFile = SaveLoadManager.instance.Settings.lastUsedSlotIndex;

            // セーブモードで、かつ最後に選択したのがオートセーブだった場合の特別処理
            if (panelName == PanelName.Save && lastFile == GameConstants.AUTO_SAVE_FILE_NUMBER)
            {
                // FileSlotInfosから手動セーブデータ（キー > 0 かつ プレイ時間 > 0）のみを対象にする
                var manualSaves = SaveLoadManager.FileSlotInfos.Where(pair =>
                    pair.Key > 0 && pair.Value != null && pair.Value.playTime > 0f
                );

                if (manualSaves.Any())
                {
                    // プレイ時間 (Value) が最も長いものを探し、そのファイル番号 (Key) を取得
                    lastFile = manualSaves.OrderByDescending(pair => pair.Value).First().Key;
                }
                else
                {
                    // 有効な手動セーブが一つもない場合は、最初のスロットを選択
                    lastFile = 1;
                }
            }

            // 表示スロット数をリストのカウントから取得 ▼▼▼
            int displaySlotCount = fileSlots.Count;
            if (displaySlotCount == 0)
            {
                Debug.LogError("fileSlotsが空です。Awake処理を確認してください。");
                return;
            }

            // もし最後に選択したのが最大ファイルかその一つ前なら、一番下のファイルが最大になるように調整
            // ロードモードで、最後に選択したのがオートセーブだった場合を考慮
            if (panelName == PanelName.Load && lastFile == GameConstants.AUTO_SAVE_FILE_NUMBER)
            {
                currentTopFileNumber = GameConstants.AUTO_SAVE_FILE_NUMBER;
            }
            else
            {
                // 範囲外の値を丸める
                if (lastFile < _startFileNumber)
                    lastFile = _startFileNumber;
                if (lastFile > _maxFileNumber)
                    lastFile = _maxFileNumber;

                // 調整ロジックをハードコード(2)から displaySlotCount に変更
                // もし最後に選択したのが最大ファイルかその手前なら、一番下のファイルが最大になるように調整
                // (例: 3スロット表示でMax 20なら、18, 19, 20 を表示する)
                int bottomSlotFileNumber = _maxFileNumber - (displaySlotCount - 1);

                if (lastFile >= bottomSlotFileNumber)
                {
                    currentTopFileNumber = bottomSlotFileNumber;
                    if (currentTopFileNumber < _startFileNumber)
                        currentTopFileNumber = _startFileNumber;
                }
                else
                {
                    currentTopFileNumber = lastFile;
                }
            }

            // 表示されているファイル番号とテキストを更新する
            UpdateDisplayedFiles();

            // 最後に選択していたボタンを自動で選択状態にする
            StartCoroutine(SetInitialSelectionCoroutine(lastFile));
        }
    }

    //他のスクリプトとの兼ね合いのため、OnDisableはコメントアウト
    // private void OnDisable() { }

    private void Update()
    {
        if (inputManager == null)
        {
            return; // InputManagerが取得できていない場合は何もしない
        }

        if (SaveLoadManager.isDataPrompting)
        {
            return; // データ変更画面が開いている場合は何もしない
        }

        // 現在選択されているGameObjectを取得
        GameObject selectedObject = EventSystem.current.currentSelectedGameObject;
        if (selectedObject == null)
            return;

        // 選択が前回と変わったフレームでは、状態を更新するだけで以降の処理は行わない
        if (selectedObject != previousSelected)
        {
            previousSelected = selectedObject;
            return; // 以降の処理は行わない
        }

        // 選択が安定しているフレームで、初めて入力処理を行う
        // --- 左右キーでのページめくり ---
        if (inputManager.UIMoveRight())
        {
            ChangePage(1); // 次のページへ
        }
        else if (inputManager.UIMoveLeft())
        {
            ChangePage(-1); // 前のページへ
        }

        // --- 上下キーでのスクロール ---
        if (
            inputManager.UIMoveUp()
            && fileSlots.Count > 0
            && selectedObject == fileSlots.First().slotObject
        )
        {
            ChangeSelectionVertical(-1); // 上へ
        }
        else if (
            inputManager.UIMoveDown()
            && fileSlots.Count > 0
            && selectedObject == fileSlots.Last().slotObject
        )
        {
            ChangeSelectionVertical(1); // 下へ
        }
    }

    /// <summary>
    /// 表示されているファイル番号とテキストを更新する
    /// </summary>
    private void UpdateDisplayedFiles()
    {
        for (int i = 0; i < fileSlots.Count; i++)
        {
            FileSlotUI slot = fileSlots[i]; // i番目のスロットに関連するUI部品がすべて入っている
            int fileNumber = currentTopFileNumber + i;

            // コンポーネントがnullでないか確認
            if (slot.slotObject == null || slot.button == null || slot.playTimeText == null)
            {
                // slotObjectがnullの場合はSetActive(false)もできないためスキップ
                // 他のコンポーネントがnullの場合はAwakeで警告済み
                continue; // 次のループへ
            }

            // ファイル番号をボタンに設定
            slot.button.FileNumber = fileNumber;

            // テキスト表示を更新
            UpdateFileSlotDisplay(slot.slotObject, fileNumber);
        }

        // 現在選択されているGameObjectを取得
        GameObject selectedObject = EventSystem.current.currentSelectedGameObject;
        // 選択されているオブジェクトがファイルボタンのいずれかであれば、そのファイル番号を保存
        if (fileSlots.Any(slot => slot.slotObject == selectedObject))
        {
            previousSelected = selectedObject;
        }
    }

    /// <summary>
    /// パネル表示時に、最後に選択されていたボタンにフォーカスを合わせるコルーチン
    /// </summary>
    private IEnumerator SetInitialSelectionCoroutine(int lastSelectedFile)
    {
        // OnEnableの直後ではUIの選択がうまくいかないことがあるため、フレームの終わりまで待つ
        yield return new WaitForEndOfFrame();

        // fileSlotsリストから、指定されたファイル番号を持つボタン（GameObject）を探す
        GameObject targetButton = null;
        targetButton = fileSlots
            .FirstOrDefault(slot =>
                slot.button != null && slot.button.FileNumber == lastSelectedFile
            )
            ?.slotObject; // 見つかったら、その slotObject を返す (null許容)

        // 該当するボタンがあれば、それを選択状態にする
        if (targetButton != null)
        {
            EventSystem.current.SetSelectedGameObject(targetButton);
        }
        else if (fileSlots.Count > 0 && fileSlots[0].slotObject != null)
        {
            // 見つからなかった場合、リストの先頭を選択する
            EventSystem.current.SetSelectedGameObject(fileSlots[0].slotObject);
        }
    }

    /// <summary>
    /// ページ単位でファイル番号を変更する（左右キー用）
    /// </summary>
    private void ChangePage(int direction)
    {
        int pageSize = fileSlots.Count; // 画面に表示されているスロット数
        if (pageSize == 0)
            return;

        currentTopFileNumber += pageSize * direction;

        // 境界チェックを動的に
        int loopAroundPoint = _maxFileNumber - (pageSize - 1);

        // 循環処理
        if (currentTopFileNumber > loopAroundPoint)
        {
            currentTopFileNumber = _startFileNumber;
        }
        else if (currentTopFileNumber < _startFileNumber)
        {
            // 最大ファイル数が pageSize の倍数でない場合も考慮し、
            // 最後のページの先頭番号である loopAroundPoint を設定
            currentTopFileNumber = loopAroundPoint;
        }

        UpdateDisplayedFiles();
    }

    /// <summary>
    /// 1つずつファイル番号を変更する（上下キー用）
    /// </summary>
    private void ChangeSelectionVertical(int direction)
    {
        if (pageSize == 0)
            return; // スロットがなければ何もしない

        currentTopFileNumber += direction;
        bool looped = false; // ループしたかどうかを判定するフラグ

        // 境界チェックを動的に
        int loopAroundPoint = _maxFileNumber - (pageSize - 1);

        // 循環処理
        if (currentTopFileNumber > loopAroundPoint)
        {
            currentTopFileNumber = _startFileNumber;
            looped = true;
        }
        else if (currentTopFileNumber < _startFileNumber)
        {
            currentTopFileNumber = loopAroundPoint;
            looped = true;
        }

        UpdateDisplayedFiles();

        if (looped)
        {
            if (direction > 0) // 下に移動してループした場合
            {
                EventSystem.current.SetSelectedGameObject(fileSlots.First().slotObject);
            }
            else // 上に移動してループした場合
            {
                EventSystem.current.SetSelectedGameObject(fileSlots.Last().slotObject);
            }
        }
    }

    // <summary>
    /// 現在表示されているスロットの中から、指定されたファイル番号に一致するスロットのUI情報を返す。
    /// </summary>
    /// <param name="fileNumber">検索するファイル番号</param>
    /// <returns>見つかったFileSlotUI。見つからなければnull。</returns>
    public FileSlotUI GetFileSlotUI(int fileNumber)
    {
        // fileSlots リストから、指定されたファイル番号を持つボタンを探す
        return fileSlots.FirstOrDefault(slot =>
            slot.button != null && slot.button.FileNumber == fileNumber
        );
    }

    /// <summary>
    /// 指定されたゲームオブジェクトのスロットに、指定されたファイル番号の情報を書き込む。
    /// （外部スクリプトからの呼び出しにも対応）
    /// </summary>
    /// <param name="fileObject">情報を書き込む対象のスロットGameObject</param>
    /// <param name="fileNumber">書き込むセーブデータのファイル番号</param>
    public void UpdateFileSlotDisplay(GameObject fileObject, int fileNumber)
    {
        // fileObject から対応する FileSlotUI を検索
        FileSlotUI slot = fileSlots.FirstOrDefault(s => s.slotObject == fileObject);

        // 検索結果の妥当性チェック
        if (slot == null)
        {
            // このパネルが管理していないGameObjectが指定された
            Debug.LogWarning(
                $"{fileObject.name} は fileSlots リストに見つかりません。",
                fileObject
            );
            return;
        }

        if (slot.playTimeText == null)
        {
            // 必要なテキストコンポーネントがキャッシュされていない（Awakeで警告済みのはず）
            return;
        }

        // 必要なコンポーネントを変数に格納（可読性のため）
        TextMeshProUGUI playTimeTextComponent = slot.playTimeText;
        TextMeshProUGUI levelTextComponent = slot.levelText; // null の可能性あり

        // 表示範囲外のファイル番号を持つボタンは非表示にする
        if (fileNumber > _maxFileNumber)
        {
            fileObject.SetActive(false);
            return;
        }
        else
        {
            fileObject.SetActive(true);
        }

        if (
            SaveLoadManager.instance != null
            && SaveLoadManager.FileSlotInfos.ContainsKey(fileNumber)
        )
        {
            string fileNameText;
            // オートセーブファイルの場合、表示名を変更
            if (fileNumber == GameConstants.AUTO_SAVE_FILE_NUMBER)
            {
                fileNameText = "オートセーブ";
            }
            else
            {
                fileNameText = "File" + fileNumber;
            }

            if (SaveLoadManager.FileSlotInfos[fileNumber].playTime == 0)
            {
                playTimeTextComponent.text = fileNameText + "\n no data ";
                // レベルテキストを空にする
                if (levelTextComponent != null)
                {
                    levelTextComponent.text = "";
                }
            }
            else
            {
                float playTime = SaveLoadManager.FileSlotInfos[fileNumber].playTime;
                int hours = Mathf.FloorToInt(playTime / 3600);
                int minutes = Mathf.FloorToInt((playTime % 3600) / 60);
                playTimeTextComponent.text = $"{fileNameText}\nプレイ時間 {hours}:{minutes:D2}";

                // レベルテキストコンポーネントがキャッシュされていれば、レベルを表示
                if (levelTextComponent != null)
                {
                    int level = PlayerLevelManager.GetLevelFromExp(
                        SaveLoadManager.FileSlotInfos[fileNumber].experience
                    );
                    levelTextComponent.text = "Lv. " + level;
                }
            }
        }
        else
        {
            // FilePlaytimeにキーが存在しない場合 (初期状態など)
            string fileNameText =
                (fileNumber == GameConstants.AUTO_SAVE_FILE_NUMBER)
                    ? "オートセーブ"
                    : "File" + fileNumber;
            playTimeTextComponent.text = fileNameText + "\n no data ";

            // レベルテキストを空にする
            if (levelTextComponent != null)
            {
                levelTextComponent.text = "";
            }
        }
    }
}
