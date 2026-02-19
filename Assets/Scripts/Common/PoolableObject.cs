using System.Collections;
using UnityEngine;

/// <summary>
/// 返却先のObjectPoolerのインスタンスを指定します
/// </summary>
public enum PoolType
{
    Persistent, // 永続プール (エフェクトなど)
    Scene // シーン用プール (敵など)
    ,
}

/// <summary>
/// ObjectPoolerで管理されるオブジェクトの共通機能をまとめた基底クラス
/// </summary>
public abstract class PoolableObject : MonoBehaviour
{
    protected string myPoolTag;
    protected PoolType returnToPool = PoolType.Persistent;

    public void SetPoolTag(string tag) => myPoolTag = tag;

    public void SetPoolType(PoolType type) => returnToPool = type;

    public string PoolTag => myPoolTag;

    /// <summary>
    /// 自分自身をプールに返却する共通ロジック
    /// </summary>
    public virtual void ReturnToPool()
    {
        // タグがない、またはプール設定がおかしい場合はDestroy
        if (string.IsNullOrEmpty(myPoolTag))
        {
            Destroy(gameObject);
            return;
        }

        bool returned = false;

        // 設定されたプールタイプに応じて返却
        if (returnToPool == PoolType.Persistent)
        {
            if (ObjectPooler.PersistentInstance != null)
            {
                ObjectPooler.PersistentInstance.ReturnToPool(myPoolTag, this.gameObject);
                returned = true;
            }
        }
        else // Scene
        {
            if (ObjectPooler.SceneInstance != null)
            {
                ObjectPooler.SceneInstance.ReturnToPool(myPoolTag, this.gameObject);
                returned = true;
            }
        }

        if (!returned)
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 遅延返却用のコルーチンヘルパー
    /// </summary>
    protected IEnumerator ReturnToPoolDelayCoroutine(float delay)
    {
        yield return new WaitForSeconds(delay);
        ReturnToPool();
    }
}
