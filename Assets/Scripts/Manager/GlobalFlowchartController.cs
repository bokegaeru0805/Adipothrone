using Fungus;
using UnityEngine;

public class GlobalFlowchartController : MonoBehaviour
{
    public static GlobalFlowchartController instance = null;
    public Flowchart globalFlowchart = null;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(this.gameObject);
            globalFlowchart = this.GetComponent<Flowchart>();
            if (globalFlowchart == null)
            {
                Debug.LogError("GlobalFlowchartControllerにFlowchartが設定されていません。", this);
            }
        }
        else
        {
            Destroy(this.gameObject);
        }
    }
}
