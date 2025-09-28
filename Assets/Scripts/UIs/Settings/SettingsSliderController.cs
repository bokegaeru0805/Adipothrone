using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

//ISelectHandlerを追加して、UIが選択されたことを検知できるようにする
public class SettingsSliderController : MonoBehaviour, IPointerUpHandler
{
    private SaveLoadManager saveLoadManager;
    private InputManager inputManager;

    [SerializeField]
    private ButtonName buttonname = ButtonName.None; // このUIの種類を指定

    [SerializeField]
    private SE_UI SEName;

    [SerializeField, Tooltip("キー操作1回あたりのスライダーの変化量")]
    private float keyStep = 0.1f;

    [SerializeField, Tooltip("長押し時に連続移動が始まるまでの待ち時間")]
    private float initialRepeatDelay = 0.5f;

    [SerializeField, Tooltip("連続移動中の入力間隔")]
    private float repeatInterval = 0.05f;

    private float nextMoveTime = 0f;
    private bool isHoldingKey = false;
    private Slider slider;

    private enum ButtonName
    {
        None = 0,
        BGMSlider = 10,
        SESlider = 20,
    }

    private void Awake()
    {
        slider = GetComponent<Slider>();
        if (slider == null)
        {
            Debug.LogError("Sliderコンポーネントが見つかりません。", this);
            return;
        }
        slider.onValueChanged.AddListener(OnSliderValueChanged);
    }

    private void OnEnable()
    {
        if (saveLoadManager == null)
        {
            saveLoadManager = SaveLoadManager.instance;
            if (saveLoadManager == null)
            {
                Debug.LogError("SaveLoadManagerが見つかりません。", this);
            }
        }

        if (inputManager == null)
        {
            inputManager = InputManager.instance;
            if (inputManager == null)
            {
                Debug.LogError("InputManagerが見つかりません。", this);
            }
        }

        //SaveLoadManagerから設定値を読み込んでスライダーに反映
        // UIが表示されたときに、現在の音量値をスライダーに反映する
        if (slider != null)
        {
            switch (buttonname)
            {
                case ButtonName.BGMSlider:
                    slider.value = saveLoadManager.Settings.bgmVolume;
                    break;
                case ButtonName.SESlider:
                    slider.value = saveLoadManager.Settings.seVolume;
                    break;
            }
        }
    }

    private void OnDisable()
    {
        if (buttonname == ButtonName.BGMSlider)
        {
            ApplyAndSaveChanges();
        }
    }

    // Updateメソッドを長押し対応ロジックに書き換える
    private void Update()
    {
        // このUIオブジェクトが現在選択されていなければ、何もしない
        if (EventSystem.current.currentSelectedGameObject != gameObject)
        {
            isHoldingKey = false; // フォーカスが外れたらリセット
            return;
        }

        // --- キーが押された瞬間の処理 ---
        if (inputManager.UIMoveRight())
        {
            MoveSlider(keyStep);
            isHoldingKey = true;
            // 次の連続移動が始まる時間をセット
            nextMoveTime = Time.unscaledTime + initialRepeatDelay;
        }
        else if (inputManager.UIMoveLeft())
        {
            MoveSlider(-keyStep);
            isHoldingKey = true;
            nextMoveTime = Time.unscaledTime + initialRepeatDelay;
        }
        // --- 長押し中の処理 ---
        else if (isHoldingKey && inputManager.UIMoveRightHold())
        {
            // 指定時間が経過していたらスライダーを動かす
            if (Time.unscaledTime >= nextMoveTime)
            {
                MoveSlider(keyStep);
                // 次の移動時間をセット
                nextMoveTime = Time.unscaledTime + repeatInterval;
            }
        }
        else if (isHoldingKey && inputManager.UIMoveLeftHold())
        {
            if (Time.unscaledTime >= nextMoveTime)
            {
                MoveSlider(-keyStep);
                nextMoveTime = Time.unscaledTime + repeatInterval;
            }
        }
        // --- キーが離された時の処理 ---
        else
        {
            isHoldingKey = false;
        }
    }

    /// <summary>
    /// スライダーを動かし、必要ならSEを鳴らす
    /// </summary>
    private void MoveSlider(float amount)
    {
        slider.value += amount;
        PlaySEIfNeeded();
    }

    /// <summary>
    /// スライダーの値が変更されたときに自動的に呼ばれるメソッド
    /// </summary>
    /// <param name="value">スライダーの新しい値</param>
    private void OnSliderValueChanged(float value)
    {
        // 種類に応じて、対応するManagerの音量を更新
        switch (buttonname)
        {
            case ButtonName.BGMSlider:
                // 1. リアルタイムの音量を変更（プレビュー用）
                BGMManager.instance?.AdjustAllVolume(value);
                // 2. 保存用データ（メモリ上）の値を更新
                saveLoadManager.Settings.bgmVolume = value;
                break;
            case ButtonName.SESlider:
                // 1. リアルタイムの音量を変更（プレビュー用）
                SEManager.instance?.AdjustAllSEVolume(value);
                // 2. 保存用データ（メモリ上）の値を更新
                saveLoadManager.Settings.seVolume = value;
                break;
        }
    }

    /// <summary>
    /// 現在のUI設定をファイルに保存します。
    /// UIの「適用」や「閉じる」ボタンのOnClickイベントから呼び出します。
    /// </summary>
    public void ApplyAndSaveChanges()
    {
        if (saveLoadManager != null)
        {
            saveLoadManager.SaveSettings();
        }
    }

    /// <summary>
    /// マウスのボタンを離したときに呼ばれる
    /// </summary>
    public void OnPointerUp(PointerEventData eventData)
    {
        // マウスを離したときにSEを再生
        PlaySEIfNeeded();
    }

    /// <summary>
    /// SEスライダーの場合のみ、SEを再生する
    /// </summary>
    private void PlaySEIfNeeded()
    {
        if (buttonname == ButtonName.SESlider)
        {
            SEManager.instance?.PlayUISE(SEName);
        }
    }

    /// <summary>
    /// オブジェクトが破棄される際に、登録したイベントを解除する（メモリリーク防止）
    /// </summary>
    private void OnDestroy()
    {
        if (slider != null)
        {
            slider.onValueChanged.RemoveListener(OnSliderValueChanged);
        }
    }
}
