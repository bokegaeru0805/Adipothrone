using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 設定画面のトグルUIのインタラクションと、関連する設定値の管理を行います。
/// このコンポーネントは、対象となるToggle UIのGameObjectにアタッチしてください。
/// </summary>
[RequireComponent(typeof(Toggle))]
public class SettingsToggleController : MonoBehaviour
{
    // このトグルが担当する設定の種類を定義
    public enum SettingType
    {
        None = 0,
        ShowControlsGuide = 1, // 操作方法UIの表示/非表示
        // 今後、VSync切り替え、フルスクリーン切り替えなどをここに追加できる
    }

    [Header("トグルの設定")]
    [Tooltip("このトグルがどの設定項目を管理するかを指定します")]
    [SerializeField] private SettingType settingType;

    [Tooltip("このトグルによって表示/非表示が切り替わるUIパネル")]
    [SerializeField] private GameObject targetUIPanel;

    private Toggle toggle;
    private SaveLoadManager saveLoadManager; // セーブ/ロード機能を使う際に有効化

    private void Awake()
    {
        // 自身のToggleコンポーネントを取得
        toggle = GetComponent<Toggle>();
        if (toggle == null)
        {
            Debug.LogError("Toggleコンポーネントが見つかりません！", this);
            this.enabled = false;
            return;
        }

        // --- イベントの登録 ---
        // トグルの値が変更されたら、OnToggleValueChangedメソッドが自動的に呼ばれるようにする
        toggle.onValueChanged.AddListener(OnToggleValueChanged);
    }

    //始めが非表示なため、マネージャーをStartで取得しようとすると失敗する
    // private void Start(){}

    /// <summary>
    /// このUIが表示されたときに呼び出されます。
    /// </summary>
    private void OnEnable()
    {

        //必要なマネージャーのインスタンスがまだ取得されていなければ、ここで取得する
        if (saveLoadManager == null)
        {
            saveLoadManager = SaveLoadManager.instance;
            if (saveLoadManager == null)
            {
                Debug.LogError("SaveLoadManagerのインスタンスが見つかりません！設定をロードできません。", this);
                return; // マネージャーが見つからなければ、ここで処理を中断
            }
        }

        // --- セーブデータから設定値を読み込み、UIの初期状態に反映 ---
        LoadSetting();
    }

    /// <summary>
    /// このUIが非表示になったときに呼び出されます。
    /// </summary>
    private void OnDisable()
    {
        // --- 現在のUIの状態をセーブデータに保存 ---
        // ※設定画面を閉じるボタンなどで明示的に保存処理を呼ぶ場合は、この処理は不要かもしれません。
        // SaveSetting();
    }

    /// <summary>
    /// トグルの値が変更されたときに呼び出されるメソッド
    /// </summary>
    /// <param name="isOn">トグルの新しい状態 (true=ON, false=OFF)</param>
    private void OnToggleValueChanged(bool isOn)
    {
        // 紐づけられたUIパネルの表示/非表示を切り替える
        if (targetUIPanel != null)
        {
            targetUIPanel.SetActive(isOn);
        }

        // 変更された値をメモリ上のセーブデータに即座に反映
        SaveSetting();

    }

    /// <summary>
    /// セーブデータから設定を読み込み、トグルの状態に反映させます。
    /// </summary>
    private void LoadSetting()
    {
        if (saveLoadManager == null || toggle == null) return;

        bool currentValue = false; // デフォルト値
        switch (settingType)
        {
            case SettingType.ShowControlsGuide:
                currentValue = saveLoadManager.Settings.isShowingControlsGuide;
                break;
        }

        // 取得した値でトグルの状態とUIパネルの表示を更新
        toggle.isOn = currentValue;
        if (targetUIPanel != null)
        {
            targetUIPanel.SetActive(currentValue);
        }
    }

    /// <summary>
    /// 現在のトグルの状態をセーブデータ（メモリ上）に保存します。
    /// </summary>
    private void SaveSetting()
    {
        if (saveLoadManager == null || toggle == null) return;

        switch (settingType)
        {
            case SettingType.ShowControlsGuide:
                saveLoadManager.Settings.isShowingControlsGuide = toggle.isOn;
                break;
        }
    }

    private void OnDestroy()
    {
        // オブジェクトが破棄される際に、登録したイベントを解除（メモリリーク防止）
        if (toggle != null)
        {
            toggle.onValueChanged.RemoveListener(OnToggleValueChanged);
        }
    }
}