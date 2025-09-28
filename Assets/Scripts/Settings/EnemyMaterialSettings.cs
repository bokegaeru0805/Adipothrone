using UnityEngine;

[CreateAssetMenu(fileName = "EnemyMaterialSettings", menuName = "Settings/Enemy Material Settings")]
public class EnemyMaterialSettings : ScriptableObject
{
    [Header("触れるとダメージを与える敵のマテリアル設定")]
    public Material DamageableEnemyMaterial;

    [Header("触れてもダメージを与えない敵のマテリアル設定")]
    public Material ImmuneEnemyMaterial;
}
