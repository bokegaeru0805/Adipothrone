using UnityEngine;

/// <summary>
/// 複数のグローバルManagerを子に持つ親オブジェクト用スクリプト。
/// ・シーン間で永続化される（DontDestroyOnLoad）
/// ・複数生成を防ぐためのSingleton構造を持つ
/// ・このオブジェクトの子にある各Managerは、すべてグローバルな責務を持つものとする
/// </summary>
public class PersistentManagers : MonoBehaviour
{
    private static PersistentManagers instance;

    private void Awake()
    {
        // 他のManagersインスタンスが存在する場合は自分を破棄
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
