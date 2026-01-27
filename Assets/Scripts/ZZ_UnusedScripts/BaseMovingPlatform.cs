// using CriWare;
// using UnityEngine;

// /// <summary>
// /// 移動するリフト（プラットフォーム）の共通基底クラス。
// /// 物理設定、乗客（プレイヤー/物理オブジェクト）の運搬、コライダー調整、SE再生を管理します。
// /// </summary>
// [RequireComponent(typeof(BoxCollider2D))]
// [RequireComponent(typeof(Rigidbody2D))]
// [RequireComponent(typeof(SpriteRenderer))]
// [RequireComponent(typeof(CriWare.Assets.CriAtomSePlayer))]
// public abstract class BaseMovingPlatform : PoolableObject
// {
//     [Header("リフト共通設定")]
//     [Tooltip("リフトの種類（SEなどに影響）")]
//     [SerializeField]
//     protected LiftType liftType = LiftType.None;

//     [Tooltip("SEをループ再生するかどうか")]
//     [SerializeField]
//     protected bool loopSE = true;

//     public enum LiftType
//     {
//         None = 0,
//         Wood = 1,
//     }

//     protected Rigidbody2D rb;
//     protected SpriteRenderer spriteRenderer;
//     protected CriWare.Assets.CriAtomSePlayer sePlayer;

//     /// <summary>
//     /// 初期化処理。継承先でoverrideする場合は base.Awake() を呼んでください。
//     /// </summary>
//     protected virtual void Awake()
//     {
//         if (liftType == LiftType.None)
//         {
//             Debug.LogWarning(
//                 $"[{gameObject.name}] LiftTypeがNoneに設定されています。適切な種類を設定してください。"
//             );
//         }

//         rb = GetComponent<Rigidbody2D>();
//         spriteRenderer = GetComponent<SpriteRenderer>();
//         sePlayer = GetComponent<CriWare.Assets.CriAtomSePlayer>();

//         // // 物理挙動の安定化設定（共通）
//         // rb.bodyType = RigidbodyType2D.Kinematic;
//         // rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
//         // rb.interpolation = RigidbodyInterpolation2D.Interpolate;

//         // コライダーサイズ調整
//         UpdateColliderSize();

//         // 初期状態では音を止める
//         StopMovingSound();
//     }

//     /// <summary>
//     /// インスペクター変更時の更新
//     /// </summary>
//     protected virtual void OnValidate()
//     {
//         UpdateColliderSize();
//     }

//     /// <summary>
//     /// SpriteRenderer (Tiled) のサイズに合わせてBoxCollider2Dを自動調整
//     /// </summary>
//     /// <summary>
//     /// SpriteRenderer (Tiled) のサイズに合わせて、物理用と検知用の両方のBoxCollider2Dを自動調整
//     /// </summary>
//     protected void UpdateColliderSize()
//     {
//         if (spriteRenderer == null)
//             spriteRenderer = GetComponent<SpriteRenderer>();

//         if (spriteRenderer == null)
//             return;

//         // 1. アタッチされている全てのBoxCollider2Dを取得
//         BoxCollider2D[] colliders = GetComponents<BoxCollider2D>();

//         foreach (var col in colliders)
//         {
//             if (col.isTrigger)
//             {
//                 // 【検知用コライダー (IsTrigger: ON)】
//                 // 物理的な床よりも「少し高く」して、プレイヤーの足元を確実に検知できるようにします。
//                 // また、「少し狭く」することで、リフトの横っ腹に触れただけで親子になるのを防ぎます。

//                 float heightBuffer = 0.2f; // 上方向へのはみ出し量
//                 float widthShrink = 0.1f; // 横幅の縮小量

//                 // サイズ設定: 幅は少し狭く、高さは少し高く
//                 col.size = new Vector2(
//                     Mathf.Max(0.1f, spriteRenderer.size.x - widthShrink),
//                     spriteRenderer.size.y + heightBuffer
//                 );

//                 // オフセット設定: 高くした分だけ中心を上にずらす（上部にはみ出させる）
//                 col.offset = new Vector2(0f, heightBuffer * 0.5f);
//             }
//             else
//             {
//                 // 【物理用コライダー (IsTrigger: OFF)】
//                 // 見た目（スプライト）のサイズと完全に一致させます。
//                 col.size = spriteRenderer.size;
//                 col.offset = Vector2.zero;
//             }
//         }
//     }

//     #region 接触管理

//     protected virtual void OnTriggerEnter2D(Collider2D other)
//     {
//         // プレイヤー または 物理オブジェクト(鉄球など) が乗ったら子要素にする
//         if (
//             other.CompareTag(GameConstants.PLAYER_TAG_NAME)
//             || other.CompareTag(GameConstants.PHYSICS_OBJECT_TAG_NAME)
//         )
//         {
//             other.transform.SetParent(this.transform);
//         }
//     }

//     protected virtual void OnTriggerExit2D(Collider2D other)
//     {
//         // 降りたら親子関係を解除
//         if (
//             other.CompareTag(GameConstants.PLAYER_TAG_NAME)
//             || other.CompareTag(GameConstants.PHYSICS_OBJECT_TAG_NAME)
//         )
//         {
//             // 自身の子供である場合のみ解除（念のため）
//             if (other.transform.parent == this.transform)
//             {
//                 other.transform.SetParent(null);
//             }
//         }
//     }
//     #endregion

//     #region SE再生管理

//     /// <summary>
//     /// 移動音の再生
//     /// </summary>
//     protected void PlayMovingSound()
//     {
//         if (loopSE && sePlayer != null && sePlayer.status != CriAtomSource.Status.Playing)
//         {
//             switch (liftType)
//             {
//                 case LiftType.Wood:
//                     sePlayer.Play(SE_Field.LiftMove_Wood);
//                     break;
//                 default:
//                     // デフォルト音
//                     break;
//             }
//         }
//     }

//     /// <summary>
//     /// 移動音の停止
//     /// </summary>

//     protected void StopMovingSound()
//     {
//         if (sePlayer != null && sePlayer.status == CriAtomSource.Status.Playing)
//         {
//             sePlayer.Stop();
//         }
//     }
//     #endregion
// }
