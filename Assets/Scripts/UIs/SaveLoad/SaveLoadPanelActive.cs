using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class SaveLoadPanelActive : MonoBehaviour
{
    [SerializeField]
    private PanelName panelName;

    [Header("セーブデータの選択ボタン")]
    [SerializeField]
    private GameObject TopFile;

    [SerializeField]
    private GameObject MiddleFile;

    [SerializeField]
    private GameObject BottomFile;
    private int currentTopFileNumber = 1; // 現在表示している一番上のファイル番号
    private InputManager inputManager;

    //ボタンのスクリプトをキャッシュしておく変数
    private SaveLoadFileButton topFileButton;
    private SaveLoadFileButton middleFileButton;
    private SaveLoadFileButton bottomFileButton;

    //1フレーム前の選択状態を記憶する変数
    private GameObject previousSelected = null;

    // モードによって変わるファイルリストの範囲を保持する変数
    private int _startFileNumber;
    private int _maxFileNumber;

    private void Awake()
    {
        if (panelName == PanelName.None)
        {
            Debug.LogWarning($"{this.gameObject.name}のパネルの名前が設定されていません");
            return;
        }

        if (TopFile == null || MiddleFile == null || BottomFile == null)
        {
            Debug.LogWarning(
                $"{this.gameObject.name}のセーブデータの選択ボタンが設定されていません"
            );
            return;
        }
        else
        {
            topFileButton = TopFile.GetComponent<SaveLoadFileButton>();
            middleFileButton = MiddleFile.GetComponent<SaveLoadFileButton>();
            bottomFileButton = BottomFile.GetComponent<SaveLoadFileButton>();
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
            //  変更点: モードに応じてファイルリストの範囲を設定
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
                // FilePlaytimeから手動セーブデータ（キー > 0 かつ プレイ時間 > 0）のみを対象にする
                var manualSaves = SaveLoadManager.FilePlaytime.Where(pair =>
                    pair.Key > 0 && pair.Value > 0
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

                // もし最後に選択したのが最大ファイルかその一つ前なら、一番下のファイルが最大になるように調整
                if (lastFile >= _maxFileNumber - 1)
                {
                    currentTopFileNumber = _maxFileNumber - 2;
                    if (currentTopFileNumber < _startFileNumber)
                        currentTopFileNumber = _startFileNumber;
                }
                else
                {
                    currentTopFileNumber = lastFile;
                }
            }

            UpdateDisplayedFiles(); // 表示されているファイル番号とテキストを更新

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
        if (inputManager.UIMoveUp() && selectedObject == TopFile)
        {
            ChangeSelectionVertical(-1); // 上へ
        }
        else if (inputManager.UIMoveDown() && selectedObject == BottomFile)
        {
            ChangeSelectionVertical(1); // 下へ
        }
    }

    /// <summary>
    /// 表示されているファイル番号とテキストを更新する
    /// </summary>
    private void UpdateDisplayedFiles()
    {
        // GetComponentの呼び出しをキャッシュした変数を使うように変更
        int topNum = currentTopFileNumber;
        int middleNum = currentTopFileNumber + 1;
        int bottomNum = currentTopFileNumber + 2;

        topFileButton.FileNumber = topNum;
        WritePlayTime(TopFile, topNum);

        middleFileButton.FileNumber = middleNum;
        WritePlayTime(MiddleFile, middleNum);

        bottomFileButton.FileNumber = bottomNum;
        WritePlayTime(BottomFile, bottomNum);

        // 現在選択されているGameObjectを取得
        GameObject selectedObject = EventSystem.current.currentSelectedGameObject;
        // 選択されているオブジェクトがファイルボタンのいずれかであれば、そのファイル番号を保存
        GameObject[] fileButtons = { TopFile, MiddleFile, BottomFile };
        if (fileButtons.Contains(selectedObject))
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

        GameObject targetButton = null;
        if (topFileButton.FileNumber == lastSelectedFile)
        {
            targetButton = TopFile;
        }
        else if (middleFileButton.FileNumber == lastSelectedFile)
        {
            targetButton = MiddleFile;
        }
        else if (bottomFileButton.FileNumber == lastSelectedFile)
        {
            targetButton = BottomFile;
        }

        // 該当するボタンがあれば、それを選択状態にする
        if (targetButton != null)
        {
            EventSystem.current.SetSelectedGameObject(targetButton);
        }
    }

    /// <summary>
    /// ページ単位でファイル番号を変更する（左右キー用）
    /// </summary>
    private void ChangePage(int direction)
    {
        const int pageSize = 3;
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
            // 最大ファイル数が3の倍数であることを前提に、最後のページの先頭番号を計算
            currentTopFileNumber = loopAroundPoint;
        }

        UpdateDisplayedFiles();
    }

    /// <summary>
    /// 1つずつファイル番号を変更する（上下キー用）
    /// </summary>
    private void ChangeSelectionVertical(int direction)
    {
        const int pageSize = 3;
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

        //ループした場合のフォーカス移動
        if (looped)
        {
            if (direction > 0) // 下に移動してループした場合
            {
                EventSystem.current.SetSelectedGameObject(TopFile);
            }
            else // 上に移動してループした場合
            {
                EventSystem.current.SetSelectedGameObject(BottomFile);
            }
        }

        UpdateDisplayedFiles();
    }

    private void WritePlayTime(GameObject fileObject, int fileNumber)
    {
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

        GameObject FileText = fileObject.transform.GetChild(0).gameObject;
        if (FileText == null)
        {
            Debug.LogWarning($"{fileObject.name}はテキストコンポーネントを持っていません");
            return;
        }

        TextMeshProUGUI textComponent = FileText.GetComponent<TextMeshProUGUI>();

        if (
            SaveLoadManager.instance != null
            && SaveLoadManager.FilePlaytime.ContainsKey(fileNumber)
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

            if (SaveLoadManager.FilePlaytime[fileNumber] == 0)
            {
                textComponent.text = fileNameText + "\n no data "; // nodate -> no data
            }
            else
            {
                float playTime = SaveLoadManager.FilePlaytime[fileNumber];
                int hours = Mathf.FloorToInt(playTime / 3600);
                int minutes = Mathf.FloorToInt((playTime % 3600) / 60);
                textComponent.text = $"{fileNameText}\nプレイ時間 {hours}:{minutes:D2}"; // D2で分を2桁表示
            }
        }
        else
        {
            // FilePlaytimeにキーが存在しない場合 (初期状態など)
            textComponent.text = "File" + fileNumber + "\n no data ";
        }
    }
}
