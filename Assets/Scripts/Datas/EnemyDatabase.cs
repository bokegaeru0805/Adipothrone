using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(fileName = "EnemyDatabase", menuName = "Enemies/Enemy Database")]
public class EnemyDatabase : ScriptableObject
{
    public List<EnemyData> enemies = new List<EnemyData>();

    // 取得を高速化するためのキャッシュ
    private Dictionary<EnemyName, EnemyData> _enemyDictionary;

    // プロパティ経由でアクセスするようにする
    public Dictionary<EnemyName, EnemyData> EnemyDictionary
    {
        get
        {
            // キャッシュがなければ作成する
            if (_enemyDictionary == null)
            {
                // ListをDictionaryに変換する。ToDictionaryは非常に便利
                _enemyDictionary = enemies.ToDictionary(enemy => enemy.enemyID);
            }
            return _enemyDictionary;
        }
    }

    // IDから敵の情報を取得するメソッド（よりシンプルかつ高速に）
    public EnemyData GetEnemyByID(EnemyName enemyID)
    {
        // Dictionaryから直接データを取得する
        if (EnemyDictionary.TryGetValue(enemyID, out EnemyData enemyData))
        {
            return enemyData;
        }
        
        // データベースに存在しないIDが指定された場合
        Debug.LogWarning($"{enemyID} に対応する敵データが見つかりません。");
        return null;
    }
}