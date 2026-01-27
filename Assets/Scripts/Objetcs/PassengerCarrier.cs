using UnityEngine;

/// <summary>
/// オブジェクトの上に乗ったプレイヤーや物理オブジェクトを親子付けして運ぶコンポーネント。
/// スプライトサイズに合わせて検知用コライダーを自動調整する機能も含みます。
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class PassengerCarrier : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        UpdateColliderSize();
    }

    /// <summary>
    /// SpriteRendererのサイズに合わせて、物理用と検知用(Trigger)のコライダーを調整する
    /// </summary>
    private void UpdateColliderSize()
    {
        if (spriteRenderer == null) return;

        BoxCollider2D[] colliders = GetComponents<BoxCollider2D>();

        foreach (var col in colliders)
        {
            if (col.isTrigger)
            {
                // 【検知用コライダー】
                // プレイヤーの足元を拾いやすくするため、物理判定より少し背を高く、
                // 横からの誤接触を防ぐため、幅を少し狭くする
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
                // 見た目通りに設定
                col.size = spriteRenderer.size;
                col.offset = Vector2.zero;
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // プレイヤーまたは物理オブジェクトなら子要素にする
        if (other.CompareTag(GameConstants.PLAYER_TAG_NAME) || 
            other.CompareTag(GameConstants.PHYSICS_OBJECT_TAG_NAME))
        {
            other.transform.SetParent(this.transform);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        // 離れたら親子関係を解除
        if (other.CompareTag(GameConstants.PLAYER_TAG_NAME) || 
            other.CompareTag(GameConstants.PHYSICS_OBJECT_TAG_NAME))
        {
            // 自分の子供である場合のみ解除（念のため）
            if (other.transform.parent == this.transform)
            {
                other.transform.SetParent(null);
            }
        }
    }

    /// <summary>
    /// 強制的に全ての乗客を降ろす（プールに戻る際などに使用）
    /// </summary>
    public void EjectAllPassengers()
    {
        // 子要素を逆順に走査して安全に解除
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child.CompareTag(GameConstants.PLAYER_TAG_NAME) || 
                child.CompareTag(GameConstants.PHYSICS_OBJECT_TAG_NAME))
            {
                child.SetParent(null);
            }
        }
    }

    // インスペクター変更時にコライダーサイズを更新
    private void OnValidate()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        UpdateColliderSize();
    }
}