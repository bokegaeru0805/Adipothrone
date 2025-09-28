using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "EnemyDatabase", menuName = "Enemies/Enemy Database")]
public class EnemyDatabase : ScriptableObject
{
    public List<EnemyData> enemies = new List<EnemyData>();

    // IDから敵の情報を取得（存在しなければnull）
    public EnemyData GetEnemyByID(Enum id)
    {
        if (id is EnemyName enemyID)
        {
            return enemies.Find(enemy => enemy.enemyID == enemyID);
        }
        return null;
    }
}