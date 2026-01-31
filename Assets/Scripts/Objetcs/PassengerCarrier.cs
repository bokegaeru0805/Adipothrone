using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// オブジェクトの上に乗ったプレイヤーや物理オブジェクトを運ぶコンポーネント。
/// プレイヤーには速度を加算し、物理オブジェクトには親子付けを行って対応します。
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class PassengerCarrier : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private Vector3 lastPosition;
    private Vector2 currentVelocity;

    // 乗っているプレイヤーのリスト（マルチプレイ対応も考慮しリスト化）
    private HashSet<Heroin_move> playerPassengers = new HashSet<Heroin_move>();

    // 離脱猶予を管理する辞書（プレイヤーごとのコルーチンを管理）
    private Dictionary<Heroin_move, Coroutine> disconnectCoroutines =
        new Dictionary<Heroin_move, Coroutine>();

    // 離脱猶予時間（秒）。0.1〜0.2秒程度で、着地バウンドによる誤判定を防ぐ
    private float disconnectDelay = 0f;

    // GC対策: WaitForSecondsのインスタンスをキャッシュする変数
    private WaitForSeconds disconnectWait;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        UpdateColliderSize();
        lastPosition = transform.position;
        disconnectWait = new WaitForSeconds(disconnectDelay);
    }

    void FixedUpdate()
    {
        // 1. 自身の移動速度を計算 (位置の差分 / 時間)
        Vector3 currentPos = transform.position;
        // ゼロ除算回避のためdeltaTimeチェック
        if (Time.fixedDeltaTime > 0)
        {
            currentVelocity = (currentPos - lastPosition) / Time.fixedDeltaTime;
        }
        lastPosition = currentPos;

        // 2. 乗っているプレイヤー全員に速度を伝達
        if (playerPassengers.Count > 0)
        {
            // HashSetを回している間にRemoveされる可能性を考慮し、コピーして回すか、
            // 安全な方法をとるが、SetCarrierVelocity内でリスト操作は発生しないためそのまま回す
            foreach (var player in playerPassengers)
            {
                if (player != null)
                {
                    player.SetCarrierVelocity(currentVelocity);
                }
            }
        }
    }

    /// <summary>
    /// SpriteRendererのサイズに合わせて、物理用と検知用(Trigger)のコライダーを調整する
    /// </summary>
    private void UpdateColliderSize()
    {
        if (spriteRenderer == null)
            return;

        BoxCollider2D[] colliders = GetComponents<BoxCollider2D>();

        foreach (var col in colliders)
        {
            if (col.isTrigger)
            {
                // 【検知用コライダー】
                float heightBuffer = 0.2f;
                float widthShrink = 0.1f;

                col.size = new Vector2(
                    Mathf.Max(0.1f, spriteRenderer.size.x - widthShrink),
                    spriteRenderer.size.y + heightBuffer
                );
                col.offset = new Vector2(0f, heightBuffer * 0.5f);
            }
            else
            {
                // 【物理用コライダー】
                col.size = spriteRenderer.size;
                col.offset = Vector2.zero;
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // プレイヤーの場合
        if (other.CompareTag(GameConstants.PLAYER_TAG_NAME))
        {
            var playerMove = other.GetComponent<Heroin_move>();
            if (playerMove != null)
            {
                // 離脱待ちのコルーチンが動いていればキャンセルする（再着地とみなす）
                if (disconnectCoroutines.ContainsKey(playerMove))
                {
                    if (disconnectCoroutines[playerMove] != null)
                    {
                        StopCoroutine(disconnectCoroutines[playerMove]);
                    }
                    disconnectCoroutines.Remove(playerMove);
                }

                // リストになければ追加
                playerPassengers.Add(playerMove);
            }
        }
        // その他の物理オブジェクトの場合：従来通り親子付けで運ぶ
        else if (other.CompareTag(GameConstants.PHYSICS_OBJECT_TAG_NAME))
        {
            other.transform.SetParent(this.transform);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        /// プレイヤーの場合
        if (other.CompareTag(GameConstants.PLAYER_TAG_NAME))
        {
            var playerMove = other.GetComponent<Heroin_move>();
            if (playerMove != null)
            {
                // 即座に外さず、猶予コルーチンを開始する
                if (playerPassengers.Contains(playerMove))
                {
                    // 既にコルーチンが走っていないか確認してから開始
                    if (!disconnectCoroutines.ContainsKey(playerMove))
                    {
                        Coroutine co = StartCoroutine(DisconnectAfterDelay(playerMove));
                        disconnectCoroutines.Add(playerMove, co);
                    }
                }
            }
        }
        // その他の物理オブジェクトの場合：親子付け解除
        else if (other.CompareTag(GameConstants.PHYSICS_OBJECT_TAG_NAME))
        {
            if (other.transform.parent == this.transform)
            {
                other.transform.SetParent(null);
            }
        }
    }

    /// <summary>
    /// プレイヤーの離脱猶予コルーチン
    /// </summary>
    /// <param name="player">離脱猶予を適用するプレイヤー</param>
    /// <returns></returns>
    private IEnumerator DisconnectAfterDelay(Heroin_move player)
    {
        // new せずにキャッシュを使用
        yield return disconnectWait;

        // 待機時間が終わってもまだ辞書に登録されている（＝再着地しなかった）場合
        if (disconnectCoroutines.ContainsKey(player))
        {
            if (playerPassengers.Contains(player))
            {
                playerPassengers.Remove(player);
                if (player != null)
                {
                    player.ExitCarrier(); // ここで初めて慣性モードへ移行
                }
            }
            disconnectCoroutines.Remove(player);
        }
    }

    /// <summary>
    /// 強制的に全ての乗客を降ろす（プールに戻る際などに使用）
    /// </summary>
    public void EjectAllPassengers()
    {
        // コルーチンも全て停止してクリーンアップ
        foreach (var kvp in disconnectCoroutines)
        {
            if (kvp.Value != null)
                StopCoroutine(kvp.Value);
        }
        disconnectCoroutines.Clear();

        // プレイヤーの解放
        foreach (var player in playerPassengers)
        {
            if (player != null)
                player.ExitCarrier();
        }
        playerPassengers.Clear();

        // 物理オブジェクトの解放（親子解除）
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child.CompareTag(GameConstants.PHYSICS_OBJECT_TAG_NAME))
            {
                child.SetParent(null);
            }
        }
    }

    private void OnValidate()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        UpdateColliderSize();
        // 遅延時間が変更されたら作り直す
        if (Application.isPlaying)
        {
            disconnectWait = new WaitForSeconds(disconnectDelay);
        }
    }
}
