using UnityEngine;

public interface IDroppable
{
    EnemyData GetEnemyData();
    Transform GetDropParent();
    Vector3 GetDropPosition();
}