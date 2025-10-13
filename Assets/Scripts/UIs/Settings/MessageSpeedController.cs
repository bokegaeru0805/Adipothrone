using UnityEngine;
using TMPro;
using UnityEngine.EventSystems;
using System;
using System.Collections.Generic;

/// <summary>
/// 会話速度の設定UIのインタラクションと設定値の管理を行います。
/// Update内で入力を監視し、UIが選択されている時に設定を変更します。
/// このコンポーネントは、UIパーツをまとめた親GameObjectにアタッチしてください。
/// </summary>
public class MessageSpeedController : MonoBehaviour
{
    [Header("UIパーツの関連付け")]
    [Tooltip("現在の設定値を表示するテキスト (TextMeshPro)")]
    [SerializeField] private TextMeshProUGUI valueText;

    // 現在の設定値を保持する変数
    private MessageSpeed currentSpeed;
    private SaveLoadManager saveLoadManager;

    // enumの選択肢を格納するリスト
    private readonly List<MessageSpeed> speedOptions = new List<MessageSpeed>();

    private void Awake()
    {
        if (valueText == null)
        {
            Debug.LogError("Value Textがアタッチされていません！", this);
            this.enabled = false;
            return;
        }

        // --- MessageSpeed enumの全ての値をリストに自動的に格納する ---
        // これにより、enumに新しい値が追加されてもコードの変更が不要になる
        foreach (MessageSpeed speed in Enum.GetValues(typeof(MessageSpeed)))
        {
            speedOptions.Add(speed);
        }
    }
    
    // UIが表示されるたびに呼ばれる
    private void OnEnable()
    {
        if (saveLoadManager == null)
        {
            saveLoadManager = SaveLoadManager.instance;
            if (saveLoadManager == null)
            {
                Debug.LogError("SaveLoadManagerのインスタンスが見つかりません！", this);
                return;
            }
        }
        LoadSetting();
    }

    private void Update()
    {
        bool isSelected = EventSystem.current.currentSelectedGameObject == this.gameObject;

        if (!isSelected)
        {
            return;
        }

        // --- InputManagerによる入力監視 ---
        
        // 左入力があった場合
        if (InputManager.instance.UIMoveLeft())
        {
            // 現在の速度がリストの何番目かを取得
            int currentIndex = speedOptions.IndexOf(currentSpeed);
            // インデックスを1つ前にずらす（0より小さくなったら末尾にループ）
            int nextIndex = (currentIndex - 1 + speedOptions.Count) % speedOptions.Count;
            currentSpeed = speedOptions[nextIndex];
            
            UpdateUIAndSave();
        }
        // 右入力があった場合
        else if (InputManager.instance.UIMoveRight())
        {
            // 現在の速度がリストの何番目かを取得
            int currentIndex = speedOptions.IndexOf(currentSpeed);
            // インデックスを1つ次にずらす（リストの要素数を超えたら先頭にループ）
            int nextIndex = (currentIndex + 1) % speedOptions.Count;
            currentSpeed = speedOptions[nextIndex];

            UpdateUIAndSave();
        }
    }

    /// <summary>
    /// セーブデータから設定を読み込みます。
    /// </summary>
    private void LoadSetting()
    {
        if (saveLoadManager == null) return;
        
        currentSpeed = saveLoadManager.Settings.messageSpeed;
        UpdateUI();
    }

    /// <summary>
    /// UIを更新し、設定を保存します。
    /// </summary>
    private void UpdateUIAndSave()
    {
        UpdateUI();
        SaveSetting();
    }

    /// <summary>
    /// 現在の設定をセーブデータ（メモリ上）に保存します。
    /// </summary>
    private void SaveSetting()
    {
        if (saveLoadManager == null) return;
        saveLoadManager.Settings.messageSpeed = currentSpeed;
    }

    /// <summary>
    /// 現在のcurrentSpeedの値に基づいて、表示テキストを更新します。
    /// </summary>
    private void UpdateUI()
    {
        switch (currentSpeed)
        {
            case MessageSpeed.Slow:
                valueText.text = "遅い";
                break;
            case MessageSpeed.Normal:
                valueText.text = "普通";
                break;
            case MessageSpeed.Fast:
                valueText.text = "速い";
                break;
            case MessageSpeed.VeryFast:
                valueText.text = "とても速い";
                break;
        }
    }
}