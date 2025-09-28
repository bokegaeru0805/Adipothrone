// using System.Collections;
// using System.Collections.Generic;
// using System.Linq;
// using Effekseer;
// using UnityEngine;

// public class Robot_wave_move : MonoBehaviour
// {
//     [SerializeField]
//     private EffekseerEffectAsset effect; // .efk を指定

//     // 敵ごとのクールタイムタイマー
//     private Dictionary<GameObject, float> enemyCooldowns = new Dictionary<GameObject, float>();
//     private int damageAmount = 0;
//     public float vanishTime { get; private set; } = 0;
//     private float maxRadius = 0;
//     private float cooldownTime = 1.0f; // クールタイム（秒）
//     public bool isStarted { get; private set; } = false; //生成が完了したかどうか
//     private int waveID; //wave武器のID
//     private CircleCollider2D circleCollider; // 円形コライダー
//     private Vector3 targetScale = new Vector3(0, 0, 0);

//     private void Awake()
//     {
//         circleCollider = this.gameObject.GetComponent<CircleCollider2D>();
//         if (circleCollider == null)
//         {
//             Debug.LogError("CircleCollider2Dがアタッチされていません。");
//         }
//     }

//     private void Start()
//     {
//         if (GameManager.instance.savedata == null)
//         {
//             Debug.LogWarning("SaveDataが存在しません");
//         }

//         waveID = GameManager
//             .instance.savedata.WeaponEquipmentData.GetAllWeaponsByType(
//                 InventoryWeaponData.WeaponType.wave
//             )
//             .Keys.FirstOrDefault(); //weaponIDを入手する
//         ;
//         if (waveID == 0)
//         {
//             Debug.LogWarning("waveは適切にIDを取得できませんでした");
//             Destroy(this.gameObject);
//             return;
//         }

//         WaveWeaponData attack = WeaponManager.instance.GetWeaponDataByID<WaveWeaponData>(waveID);
//         if (attack != null)
//         {
//             damageAmount = attack.power;
//             vanishTime = attack.vanishTime;
//             maxRadius = attack.maxRadius;
//             cooldownTime = attack.cooldownTime;
//         }

//         isStarted = true; //生成が完了した
//         float currentWidth = GetComponent<Renderer>().bounds.size.x;
//         float scaleRatio = (maxRadius * 2) / currentWidth;
//         targetScale = transform.localScale * scaleRatio;
//         StartCoroutine(ResizeCoroutine(maxRadius * 2, vanishTime));
//         Destroy(this.gameObject, vanishTime);
//     }

//     private void Update()
//     {
//         // タイマーを減らす（必要に応じてクリア）
//         List<GameObject> toRemove = new List<GameObject>();

//         foreach (var key in enemyCooldowns.Keys.ToList())
//         {
//             enemyCooldowns[key] -= Time.deltaTime;
//             if (enemyCooldowns[key] <= 0f)
//             {
//                 enemyCooldowns.Remove(key);
//             }
//         }
//     }

//     private void OnTriggerStay2D(Collider2D collision)
//     {
//         if (collision.CompareTag("DamageableEnemy") || collision.CompareTag("ImmuneEnemy"))
//         {
//             GameObject enemy = collision.gameObject;

//             // まだクールタイム中なら何もしない
//             if (enemyCooldowns.ContainsKey(enemy))
//                 return;
//             // クールタイム開始
//             enemyCooldowns[enemy] = cooldownTime;

//             // 衝突点（自分のCollider上の、collisionに最も近い点）
//             Vector2 contactPoint = circleCollider.ClosestPoint(collision.transform.position);
//             if (effect != null)
//             {
//                 var handle = EffekseerSystem.PlayEffect(effect, contactPoint);
//                 //エフェクトを再生
//             }

//             // ヒット処理（例：ダメージを与える）
//             IDamageable hpScript = collision.gameObject.GetComponent<IDamageable>();
//             if (hpScript != null)
//             {
//                 hpScript.Damage(damageAmount); // ダメージ量を指定
//                 return;
//             }
//         }
//     }

//     private IEnumerator ResizeCoroutine(float targetWidth, float duration)
//     {
//         SpriteRenderer rend = GetComponent<SpriteRenderer>();
//         float currentWidth = rend.bounds.size.x;
//         float scaleRatio = targetWidth / currentWidth;

//         Vector3 initialScale = transform.localScale;
//         Vector3 targetScale = initialScale * scaleRatio;

//         float timeElapsed = 0f;

//         while (timeElapsed < duration)
//         {
//             float t = timeElapsed / duration;
//             transform.localScale = Vector3.Lerp(initialScale, targetScale, t);
//             timeElapsed += Time.deltaTime;
//             yield return null;
//         }

//         transform.localScale = targetScale;
//     }
// }