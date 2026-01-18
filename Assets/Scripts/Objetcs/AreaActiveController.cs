using System.Collections;
using UnityEngine;

/// <summary>
/// CameraMoveArea内にいる間だけ自身をアクティブにし、
/// エリア外に出たら自動的に非アクティブ化（プール返却など）を行うコンポーネント。
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class AreaActiveController : PoolableObject
{
    /// <summary>出現時、エリア外にいても即座に消えずに待機する猶予時間（秒）</summary>
    private const float SPAWN_GRACE_DURATION = 0.5f;

    /// <summary>エリアから出た後、非アクティブ化されるまでの遅延時間（秒）</summary>
    private const float EXIT_DEACTIVATE_DELAY = 0.5f;

    [Header("寿命設定")]
    [Tooltip("出現してから強制的に消滅するまでの時間（秒）。0以下の場合は無効（時間で消えない）。")]
    [SerializeField]
    private float lifetime = 0f;

    [Tooltip("trueの場合、非アクティブ化の代わりにDestroyする（プールを使わない場合など）")]
    [SerializeField]
    private bool destroyOnExit = false;

    // 現在自身が所属している（監視対象の）エリア
    private CameraMoveArea currentRegisteredArea;

    // 猶予期間チェック用のコルーチン
    private Coroutine checkAreaCoroutine;

    // 寿命チェック用コルーチン変数
    private Coroutine lifetimeCoroutine;

    private void OnEnable()
    {
        currentRegisteredArea = null;

        // 出現直後にエリア内にいるかチェックを開始
        // 少し猶予を持たせることで、生成位置が微妙にズレていても即死しないようにする
        if (checkAreaCoroutine != null)
            StopCoroutine(checkAreaCoroutine);
        checkAreaCoroutine = StartCoroutine(CheckInitialAreaEntry());

        // 寿命が設定されている場合、カウントダウンを開始
        if (lifetime > 0f)
        {
            if (lifetimeCoroutine != null)
                StopCoroutine(lifetimeCoroutine);
            lifetimeCoroutine = StartCoroutine(LifetimeCheck());
        }
    }

    private void OnDisable()
    {
        // 登録情報の解除
        currentRegisteredArea = null;
        if (checkAreaCoroutine != null)
        {
            StopCoroutine(checkAreaCoroutine);
            checkAreaCoroutine = null;
        }

        // 寿命コルーチンを停止
        if (lifetimeCoroutine != null)
        {
            StopCoroutine(lifetimeCoroutine);
            lifetimeCoroutine = null;
        }
    }

    /// <summary>
    /// 初期化時にエリアへの登録を試みるコルーチン
    /// </summary>
    private IEnumerator CheckInitialAreaEntry()
    {
        float timer = 0f;

        // 猶予期間の間、エリアに登録されるのを待つ
        while (timer < SPAWN_GRACE_DURATION)
        {
            // 既に何らかのエリアに登録されたら成功として終了
            if (currentRegisteredArea != null)
            {
                yield break;
            }

            timer += Time.deltaTime;
            yield return null;
        }

        // 猶予期間を過ぎてもエリアに登録されなかった場合
        if (currentRegisteredArea == null)
        {
            // Debug.LogWarning($"{name}: 猶予期間内にCameraMoveAreaに入らなかったため、非アクティブ化します。", this);
            DeactivateSelf();
        }
    }

    /// <summary>
    /// Trigger接触判定：CameraMoveAreaに入ったとき
    /// </summary>
    private void OnTriggerEnter2D(Collider2D other)
    {
        // 接触した相手がCameraMoveAreaか確認
        // (CameraMoveAreaスクリプトがついているか、もしくはタグなどで判定しても良い)
        var area = other.GetComponent<CameraMoveArea>();
        if (area != null)
        {
            // 新しいエリアに入ったら登録を更新
            // ※複数のエリアが重なっている場合、最後に入った方を優先する挙動になります
            currentRegisteredArea = area;
        }
    }

    /// <summary>
    /// Trigger退出判定：CameraMoveAreaから出たとき
    /// </summary>
    private void OnTriggerExit2D(Collider2D other)
    {
        // 登録中のエリアから出たかどうかを確認
        var area = other.GetComponent<CameraMoveArea>();
        if (area != null && area == currentRegisteredArea)
        {
            // エリアから出たので、非アクティブ化処理を開始
            StartCoroutine(DeactivateWithDelay());
        }
    }

    /// <summary>
    /// 寿命チェック用コルーチン
    /// </summary>
    private IEnumerator LifetimeCheck()
    {
        yield return new WaitForSeconds(lifetime);
        DeactivateSelf();
    }

    /// <summary>
    /// 遅延付きで自身を非アクティブ化（または破棄）する
    /// </summary>
    private IEnumerator DeactivateWithDelay()
    {
        // 既に登録エリアが変わっている（別のエリアに移動した）場合はキャンセル
        // または、非アクティブ化待ちの間に再度同じエリアに戻ってきた場合も考慮したいが、
        // ここではシンプルに「出た瞬間のエリア」とのリンクを切る処理とする。

        CameraMoveArea exitedArea = currentRegisteredArea;
        currentRegisteredArea = null; // 登録解除

        if (EXIT_DEACTIVATE_DELAY > 0f)
        {
            yield return new WaitForSeconds(EXIT_DEACTIVATE_DELAY);
        }

        // 待機中に別のエリアに入っていたらセーフ（消さない）
        if (currentRegisteredArea != null)
        {
            yield break;
        }

        DeactivateSelf();
    }

    /// <summary>
    /// 自身を非アクティブ化、または破棄する
    /// </summary>
    private void DeactivateSelf()
    {
        // プールタグが設定されていればプールに戻す
        if (!string.IsNullOrEmpty(myPoolTag))
        {
            ReturnToPool(); // PoolableObjectのメソッドを呼び出す
        }
        else // プールタグが設定されていなければDestroyまたは非アクティブ化
        {
            if (destroyOnExit)
            {
                Destroy(this.gameObject);
            }
            else
            {
                this.gameObject.SetActive(false);
            }
        }
    }
}
