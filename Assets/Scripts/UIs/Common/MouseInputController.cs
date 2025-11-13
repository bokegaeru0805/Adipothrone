using UnityEngine;
using UnityEngine.UI; // GraphicRaycaster を使うために必要

public class MouseInputController : MonoBehaviour
{
    [SerializeField]
    [Tooltip("マウス操作を無効にしたいCanvas。指定しない場合、自動で探します。")]
    private Canvas targetCanvas;

    private GraphicRaycaster graphicRaycaster;

    void Start()
    {
        // Canvasがインスペクターから指定されていなければ、
        // このスクリプトがアタッチされているオブジェクトから探す
        if (targetCanvas == null)
        {
            targetCanvas = GetComponent<Canvas>();
        }

        if (targetCanvas != null)
        {
            // Canvasにアタッチされている GraphicRaycaster を取得
            graphicRaycaster = targetCanvas.GetComponent<GraphicRaycaster>();

            if (graphicRaycaster == null)
            {
                Debug.LogError("対象のCanvasに GraphicRaycaster が見つかりませんでした。", this);
                return;
            }
        }
        else
        {
            Debug.LogError("対象のCanvasが見つかりません。", this);
            return;
        }

        // 初期状態を設定
        bool isMouseEnabled = SaveLoadManager.instance?.Settings.isMouseInputEnabled ?? true;
        SetMouseInputActive(isMouseEnabled);
    }

    /// <summary>
    /// UIに対するマウス操作の可否を設定します（キーボード操作には影響しません）。
    /// </summary>
    /// <param name="isMouseEnabled">trueで許可, falseで無効</param>
    public void SetMouseInputActive(bool isMouseEnabled)
    {
        if (graphicRaycaster != null)
        {
            // GraphicRaycaster の有効/無効を切り替える
            graphicRaycaster.enabled = isMouseEnabled;
        }
    }

    // --- テスト用のコード（必要に応じて使用）---
    /*
    void Update()
    {
        // Mキーでマウスの有効/無効を切り替えるテスト
        if (Input.GetKeyDown(KeyCode.M))
        {
            if (graphicRaycaster != null)
            {
                SetMouseInputActive(!graphicRaycaster.enabled);
                Debug.Log("マウス操作を " + graphicRaycaster.enabled + " に切り替えました。");
            }
        }
    }
    */
}
