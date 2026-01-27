// using System.Collections.Generic;
// using UnityEngine;

// /// <summary>
// /// PathMovementSpawnerによって制御される、経路移動型リフト。
// /// </summary>
// public class PathMovementPlatform : BaseMovingPlatform
// {
//     private List<Vector3> pathPoints;
//     private int currentTargetIndex = 0;
//     private float moveSpeed;
//     private bool isInitialized = false;

//     // AwakeはBaseで処理されるので省略可能ですが、追加初期化があれば書きます
//     // 今回は追加がないので省略します

//     /// <summary>
//     /// Spawnerから呼ばれる初期化処理
//     /// </summary>
//     public void Initialize(List<Vector3> worldPath, float speed, string tag, PoolType type)
//     {
//         pathPoints = new List<Vector3>(worldPath);
//         moveSpeed = speed;
//         myPoolTag = tag;
//         returnToPool = type;

//         currentTargetIndex = 1;

//         if (pathPoints.Count > 0)
//         {
//             transform.position = pathPoints[0];
//         }

//         isInitialized = true;
//         PlayMovingSound(); // 移動音開始
//     }

//     private void FixedUpdate()
//     {
//         if (!isInitialized || pathPoints == null || pathPoints.Count <= 1)
//             return;

//         if (currentTargetIndex >= pathPoints.Count)
//         {
//             ReturnSelf();
//             return;
//         }

//         MoveAlongPath();
//     }

//     /// <summary>
//     /// 経路に沿って移動する
//     /// </summary>
//     private void MoveAlongPath()
//     {
//         Vector3 targetPos = pathPoints[currentTargetIndex];
//         Vector2 currentPos = rb.position;
//         float distance = Vector2.Distance(currentPos, targetPos);
//         float step = moveSpeed * Time.fixedDeltaTime;

//         if (distance <= step)
//         {
//             rb.MovePosition(targetPos);
//             currentTargetIndex++;
//         }
//         else
//         {
//             Vector2 direction = ((Vector2)targetPos - currentPos).normalized;
//             rb.MovePosition(currentPos + direction * step);
//         }
//     }

//     /// <summary>
//     /// 自分自身をプールに返却する
//     /// </summary>
//     private void ReturnSelf()
//     {
//         isInitialized = false;
//         StopMovingSound();

//         // 乗っているオブジェクトを強制解除（BaseクラスのTriggerExitだけでは賄えないケースへの保険）
//         foreach (Transform child in transform)
//         {
//             if (
//                 child.CompareTag(GameConstants.PLAYER_TAG_NAME)
//                 || child.CompareTag(GameConstants.PHYSICS_OBJECT_TAG_NAME)
//             )
//             {
//                 child.SetParent(null);
//             }
//         }

//         ReturnToPool();
//     }
// }
