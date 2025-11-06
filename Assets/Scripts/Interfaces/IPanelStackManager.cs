using UnityEngine;

/// <summary>
/// UIManagerなど、パネルスタックの開閉を管理するクラスが共通して実装するインターフェース
/// </summary>
public interface IPanelStackManager
{
    /// <summary>
    /// スタックの最前面にあるパネルを閉じます。
    /// </summary>
    void CloseTopPanel();

    /// <summary>
    /// 新しいパネルを開き、スタックに追加します。
    /// </summary>
    /// <param name="panel">開く対象のGameObject</param>
    /// <param name="Stage">スタックの階層（-1の場合は単純追加）</param>
    void OpenPanel(GameObject panel, int Stage);
}