using Fungus;
using UnityEngine;

public static class FungusHelper
{
    public static void ExecuteBlock(Flowchart flowchart, string blockName)
    {
        if (flowchart == null)
        {
            Debug.LogWarning("Flowchartがnullです。");
            return;
        }

        if (flowchart.HasBlock(blockName))
        {
            flowchart.ExecuteBlock(blockName);
        }
        else
        {
            Debug.LogWarning($"Fungus Block '{blockName}' が Flowchart '{flowchart.name}' に見つかりません。");
        }
    }
}