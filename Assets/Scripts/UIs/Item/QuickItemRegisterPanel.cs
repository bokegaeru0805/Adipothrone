using System;
using System.Collections;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.EventSystems;

public class QuickItemRegisterPanel : MonoBehaviour
{
    [HideInInspector]
    public Enum itemID;

    [InfoBox("必ずこのゲームオブジェクトは初期状態で表示にしてください。")]
    [SerializeField]
    private QuickItemPanel quickItemPanel; //ゲーム画面のショートカットパネルのオブジェクト
    private GameObject lastSelectedObject; //最後に選ばれていたボタンを保存する変数

    private int activeSortOrder = 21; // 表示中に設定するSortOrderの値
    private int originalSortOrder; // 元のSortOrderを記憶しておく変数
    private Canvas quickItemCanvas; // QuickItemPanelのCanvasコンポーネント

    private void Awake()
    {
        if (quickItemPanel == null)
        {
            Debug.LogError(
                "QuickItemRegisterPanel: quickItemPanel の参照が設定されていません。",
                this
            );
        }
    }

    private void Update()
    {
        if (quickItemPanel == null)
            return; //クイックアイテムパネルが存在しない場合は何もしない

        if (InputManager.instance.UIMoveLeft())
            Move(-1);
        if (InputManager.instance.UIMoveRight())
            Move(1);
        if (InputManager.instance.UIMoveUp() || InputManager.instance.UIMoveDown())
            MoveVertical();
        if (InputManager.instance.UIConfirm())
            HandleYes();
        if (InputManager.instance.UISelectNo())
            HandleNo();
    }

    /// <summary>
    /// 親パネル（HealItemPanel等）の「1フレーム遅延フォーカス復旧処理」を上書きするため、
    /// こちらも少し待ってからフォーカスを完全にクリア（null）にします。
    /// </summary>
    private IEnumerator ClearFocusAfterDelay()
    {
        yield return new WaitForEndOfFrame();
        yield return null;

        EventSystem.current.SetSelectedGameObject(null);
    }

    /// <summary>
    /// 水平方向にカーソルを移動させる（左右）
    /// </summary>
    /// <param name="horizontal">-1なら左、+1なら右に移動</param>
    private void Move(int horizontal)
    {
        quickItemPanel.Move(horizontal);
    }

    /// <summary>
    /// 垂直方向にカーソルを移動させる（下に進む）
    /// 現在の行の下の行へ移動。最下行の場合は一番上にループする。
    /// </summary>
    private void MoveVertical()
    {
        quickItemPanel.MoveVertical();
    }

    private void HandleYes()
    {
        PlayerManager.instance?.AssignItemToQuickSlot(itemID, quickItemPanel.currentIndex); //アイテムをクイックスロットに登録
        ClosePanel();
    }

    private void HandleNo()
    {
        ClosePanel();
    }

    private void ClosePanel()
    {
        // 記憶しておいた元のSortOrderに戻す
        if (quickItemCanvas != null)
        {
            quickItemCanvas.sortingOrder = originalSortOrder;
        }

        UIManager.instance.ClosePopup(); //このパネルはUIManagerのスタックに積まない独立したポップアップとして扱うため、ClosePopup()で閉じる
        UIManager.instance.SetQuickItemRegistering(false); //クイックアイテム登録画面が開いているフラグを下げる
        UIManager.instance.RefocusTopPanel(); //親パネルにフォーカスを戻す
    }

    private void OnEnable()
    {
        UIManager.instance.SetQuickItemRegistering(true); //クイックアイテム登録画面が開いているフラグを立てる

        // QuickItemPanelのCanvasを取得してSortOrderを変更する
        if (quickItemPanel != null)
        {
            quickItemCanvas = quickItemPanel.GetComponent<Canvas>();
            if (quickItemCanvas != null)
            {
                originalSortOrder = quickItemCanvas.sortingOrder; // 元の値を保存
                quickItemCanvas.sortingOrder = activeSortOrder; // 新しい値を設定
            }
            else
            {
                Debug.LogWarning("QuickItemPanelにCanvasコンポーネントが見つかりません。");
            }
        }

        StartCoroutine(ClearFocusAfterDelay());
    }
}
