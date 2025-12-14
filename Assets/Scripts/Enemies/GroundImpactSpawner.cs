using System.Collections.Generic;
using NaughtyAttributes;
using UnityEngine;

/// <summary>
/// 地面に接触した際、指定されたオブジェクト群を指定された速度で生成・放出するスクリプト。
/// このスクリプトに自動でプールに返却する機能は含まれていません。
/// 必要に応じて別途PoolableObjectLifecycleなどを併用してください
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class GroundImpactSpawner : MonoBehaviour
{
    [InfoBox(
        "地面に接触した際、指定されたオブジェクト群を指定された速度で生成・放出するスクリプト。\n"
            + "このスクリプトに自動でプールに返却する機能は含まれていません。\n"
            + "必要に応じて別途PoolableObjectLifecycleなどを併用してください",EInfoBoxType.Warning
    )]
    [ReadOnly]
    [SerializeField] // ReadOnlyで編集不可にしておくと説明用っぽくなる
    private string _instruction = "設定不要";

    [System.Serializable]
    public class SpawnItem
    {
        public string poolTag;

        [Tooltip("ObjectPoolerの種類（タグ指定時のみ有効）")]
        public PoolType poolType = PoolType.Scene;

        [Tooltip("与える速度ベクトル")]
        public Vector2 velocity;

        [Tooltip("生成時の向きを速度ベクトルの方向に向けるか")]
        public bool alignToVelocity = true;
    }

    [Header("生成設定")]
    [Tooltip("地面接触時に生成するオブジェクトと速度のリスト")]
    [SerializeField]
    private List<SpawnItem> spawnItems = new List<SpawnItem>();

    [Header("判定設定")]
    [Tooltip("自分の向き（FlipXなど）に合わせて、生成物のX速度も反転させるか")]
    [SerializeField]
    private bool flipVelocityX = true;

    private LayerMask groundLayer;
    private SpriteRenderer spriteRenderer;

    private void Awake()
    {
        // Groundレイヤーを取得
        groundLayer = LayerMask.GetMask(GameConstants.PhysicsLayerName_Ground);
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // トリガー同士の衝突は無視
        if (other.isTrigger)
            return;

        // 相手のレイヤーがgroundLayerに含まれているか確認
        if ((groundLayer.value & (1 << other.gameObject.layer)) == 0)
            return;

        SpawnDebris();
    }

    /// <summary>
    /// リストに登録されたオブジェクトを生成し、速度を与える処理
    /// </summary>
    private void SpawnDebris()
    {
        // 向き判定（SpriteRendererがFlipX、またはScale.xが負の場合に左向きとみなす）
        bool isFacingLeft = false;
        if (spriteRenderer != null && spriteRenderer.flipX)
            isFacingLeft = true;
        else if (transform.lossyScale.x < 0)
            isFacingLeft = true;

        foreach (var item in spawnItems)
        {
            GameObject spawnedObj = null;

            // 1. 生成処理（プール or Instantiate）
            if (!string.IsNullOrEmpty(item.poolTag))
            {
                // プールから取得
                if (item.poolType == PoolType.Scene && ObjectPooler.SceneInstance != null)
                {
                    spawnedObj = ObjectPooler.SceneInstance.SpawnFromPool(
                        item.poolTag,
                        transform.position,
                        Quaternion.identity
                    );
                }
                else if (
                    item.poolType == PoolType.Persistent
                    && ObjectPooler.PersistentInstance != null
                )
                {
                    spawnedObj = ObjectPooler.PersistentInstance.SpawnFromPool(
                        item.poolTag,
                        transform.position,
                        Quaternion.identity
                    );
                }
            }

            // // プール未設定、またはプール取得失敗時はInstantiate
            // if (spawnedObj == null && item.prefab != null)
            // {
            //     spawnedObj = Instantiate(item.prefab, transform.position, Quaternion.identity);
            // }

            // 2. 速度適用処理
            if (spawnedObj != null)
            {
                // 向きに合わせてX速度を反転
                Vector2 finalVelocity = item.velocity;
                if (flipVelocityX && isFacingLeft)
                {
                    finalVelocity.x *= -1;
                }

                // Rigidbody2Dを取得して速度を設定
                Rigidbody2D rb = spawnedObj.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    rb.velocity = finalVelocity;
                }
                else
                {
                    Debug.LogWarning(
                        $"生成されたオブジェクト {spawnedObj.name} にRigidbody2Dがありません。速度を適用できませんでした。"
                    );
                }

                // --- 向きの適用 ---
                if (item.alignToVelocity)
                {
                    // 速度ベクトルから角度(度数)を算出 (Atan2はラジアンを返すのでDegに変換)
                    // ※スプライトの右向き(Vector2.right)を基準0度とします
                    float angle = Mathf.Atan2(finalVelocity.y, finalVelocity.x) * Mathf.Rad2Deg;
                    spawnedObj.transform.rotation = Quaternion.Euler(0, 0, angle);
                }
                else
                {
                    // 回転させない場合のみ、FlipXでの反転処理を行う
                    // (回転とFlipXを併用すると上下反転などの原因になるため排他制御)
                    if (flipVelocityX && isFacingLeft)
                    {
                        var sr = spawnedObj.GetComponent<SpriteRenderer>();
                        if (sr != null)
                            sr.flipX = true; // 左向きにする
                    }
                }
            }
        }
    }
}
