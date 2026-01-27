using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawner制御による経路移動コンポーネント。
/// PoolableObjectを継承し、プール管理に対応します。
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PathMover : PoolableObject // BaseMovingPlatformではなくPoolableObjectを直接継承
{
    private List<Vector3> pathPoints;
    private int currentTargetIndex = 0;
    private float moveSpeed;
    private bool isInitialized = false;

    private Rigidbody2D rb;
    private MovingPlatformAudio platformAudio;
    private PassengerCarrier carrier;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        platformAudio = GetComponent<MovingPlatformAudio>();
        carrier = GetComponent<PassengerCarrier>();
    }

    public void Initialize(List<Vector3> worldPath, float speed, string tag, PoolType type)
    {
        pathPoints = new List<Vector3>(worldPath);
        moveSpeed = speed;
        myPoolTag = tag;
        returnToPool = type;

        currentTargetIndex = 1;

        if (pathPoints.Count > 0)
        {
            transform.position = pathPoints[0];
        }

        isInitialized = true;
        if (platformAudio != null) platformAudio.PlayMoveSound();
    }

    void FixedUpdate()
    {
        if (!isInitialized || pathPoints == null || pathPoints.Count <= 1)
            return;

        if (currentTargetIndex >= pathPoints.Count)
        {
            ReturnSelf();
            return;
        }

        MoveAlongPath();
    }

    private void MoveAlongPath()
    {
        Vector3 targetPos = pathPoints[currentTargetIndex];
        Vector2 currentPos = rb.position;
        float distance = Vector2.Distance(currentPos, targetPos);
        float step = moveSpeed * Time.fixedDeltaTime;

        if (distance <= step)
        {
            rb.MovePosition(targetPos);
            currentTargetIndex++;
        }
        else
        {
            Vector2 direction = ((Vector2)targetPos - currentPos).normalized;
            rb.MovePosition(currentPos + direction * step);
        }
    }

    private void ReturnSelf()
    {
        isInitialized = false;
        
        if (platformAudio != null) platformAudio.StopMoveSound();

        // PassengerCarrierを使って乗客を安全に降ろす
        if (carrier != null)
        {
            carrier.EjectAllPassengers();
        }

        ReturnToPool();
    }
}