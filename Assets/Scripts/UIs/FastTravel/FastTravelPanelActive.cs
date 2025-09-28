using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// ファストトラベルパネルの表示と機能を管理します。
/// このパネルは通常非アクティブで、OpenPanel()メソッドによって表示されます。
/// </summary>
public class FastTravelPanelActive : MonoBehaviour
{
    private FastTravelManager fastTravelManager; //ファストトラベルマネージャー

    [Header("選択ボタンコンポーネント")]
    [SerializeField]
    private List<Button> locationButtons; //ファストトラベル選択用のボタンのリスト

    // パフォーマンス向上のため、ボタンの補助スクリプトを事前にキャッシュ
    private List<FastTravelSelectButton> cachedSelectButtons;

    // プレイヤーが所持しているファストトラベルの情報のリスト。
    // 各要素は FastTravelEntry として、ファストトラベルのID（fastTravelId）を保持する。
    private List<FastTravelPointData> availableTravelPoints = new List<FastTravelPointData>();

    private void Awake()
    {
        // 必須コンポーネントのnullチェック
        if (locationButtons == null || locationButtons.Count == 0)
        {
            Debug.LogError("FastTravelPanelActiveの必須コンポーネントが設定されていません。", this);
            gameObject.SetActive(false); // エラー時は自身を無効化
            return;
        }

        // FastTravelManagerの参照を取得
        fastTravelManager = FindAnyObjectByType<FastTravelManager>();
        if (fastTravelManager == null)
        {
            Debug.LogError("FastTravelManagerが見つかりません。", this);
            return;
        }

        if (this.name != GameConstants.UIName_FastTravelPanel)
        {
            Debug.LogError(
                "FastTravelPanelActiveは"
                    + GameConstants.UIName_FastTravelPanel
                    + "という名前である必要があります"
            );
            return;
        }

        // パフォーマンス向上のため、ボタンにアタッチされた補助スクリプトを事前に取得してキャッシュする
        CacheButtonComponents();
    }

    /// <summary>
    /// パネルがアクティブな間、毎フレーム呼ばれます。
    /// </summary>
    private void Update()
    {
        if (InputManager.instance.UISelectNo())
        { // パネルを閉じる処理
            CloseFastTravelPanel();
        }
    }

    /// <summary>
    /// このパネルを開き、UIを初期化します。外部からこのメソッドを呼び出してください。
    /// </summary>
    public void OpenFastTravelPanel()
    {
        // まずパネル自体を表示する
        gameObject.SetActive(true);

        // 時間を一時停止
        TimeManager.instance.RequestPause();

        // ボタンリストを最新の情報で更新
        RefreshButtonList();
    }

    public void CloseFastTravelPanel()
    {
        TimeManager.instance.ReleasePause(); // 時間を再開
        EventSystem.current.SetSelectedGameObject(null); // ボタンの選択状態を解除
        this.gameObject.SetActive(false); // パネルを非表示にする
    }

    /// <summary>
    /// locationButtonsにアタッチされたFastTravelSelectButtonをキャッシュします。
    /// </summary>
    private void CacheButtonComponents()
    {
        cachedSelectButtons = new List<FastTravelSelectButton>();
        foreach (var button in locationButtons)
        {
            var selectButton = button.GetComponent<FastTravelSelectButton>();
            if (selectButton != null)
            {
                cachedSelectButtons.Add(selectButton);
            }
            else
            {
                Debug.LogError(
                    "ボタンにFastTravelSelectButtonコンポーネントがありません。",
                    button
                );
            }
        }
    }

    /// <summary>
    /// 利用可能なファストトラベル先を取得し、ボタンの表示を更新します。
    /// </summary>
    private void RefreshButtonList()
    {
        // 1. 利用可能なファストトラベル先のデータを取得・更新
        availableTravelPoints.Clear();
        var fastTravelData = GameManager.instance?.savedata?.FastTravelData;
        if (fastTravelData != null)
        {
            foreach (var entry in fastTravelData.unlockedFastTravels)
            {
                var pointData = fastTravelManager.GetFastTravelPointData(
                    (FastTravelName)entry.FastTravelID
                );
                if (pointData != null)
                {
                    availableTravelPoints.Add(pointData);
                }
            }
        }

        // 2. 取得したデータに基づいてボタンの表示を更新
        int availablePointCount = availableTravelPoints.Count;
        for (int i = 0; i < cachedSelectButtons.Count; i++)
        {
            var button = cachedSelectButtons[i];
            // 表示すべきデータがある場合
            if (i < availablePointCount)
            {
                var pointData = availableTravelPoints[i];
                button.UpdateFastTravelName(pointData.fastTravelName);
                button.AssignItem(pointData.fastTravelId);
                button.gameObject.SetActive(true);
            }
            // 表示すべきデータがない場合はボタンを非表示
            else
            {
                button.gameObject.SetActive(false);
            }
        }

        // 3. 最初の利用可能なボタンを選択状態にする
        if (availablePointCount > 0)
        {
            EventSystem.current.SetSelectedGameObject(cachedSelectButtons[0].gameObject);
        }
    }

    public void RequestFastTravel(Enum fastTravelId)
    {
        CloseFastTravelPanel(); // パネルを閉じる
        // ファストトラベルを実行
        fastTravelManager?.ExecuteFastTravel(fastTravelId);
        SEManager.instance?.PlaySystemEventSE(SE_SystemEvent.Warp1); // ワープ音を再生
    }
}