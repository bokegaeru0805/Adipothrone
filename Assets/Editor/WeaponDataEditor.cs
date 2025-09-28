using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ShootWeaponData))]
public class ShootWeaponDataEditor : Editor
{
    SerializedProperty weaponID;
    SerializedProperty power;
    SerializedProperty cooldownTime;
    SerializedProperty shootSpeed;
    SerializedProperty vanishTime;
    SerializedProperty shotInterval;
    SerializedProperty penetrationLimitCount;
    SerializedProperty moveType;
    SerializedProperty colliderOffset;
    SerializedProperty colliderRadius;
    SerializedProperty shootAnimation;

    // WeaponData（親クラス）のプロパティ
    SerializedProperty itemName;
    SerializedProperty itemSprite;
    SerializedProperty itemRank;
    SerializedProperty buyPrice;
    SerializedProperty sellPrice;
    SerializedProperty description;
    SerializedProperty wpCost;

    void OnEnable()
    {
        // ShootWeaponData独自
        weaponID = serializedObject.FindProperty("weaponID");
        power = serializedObject.FindProperty("power");
        cooldownTime = serializedObject.FindProperty("cooldownTime");
        shootSpeed = serializedObject.FindProperty("shootSpeed");
        vanishTime = serializedObject.FindProperty("vanishTime");
        shotInterval = serializedObject.FindProperty("shotInterval");
        penetrationLimitCount = serializedObject.FindProperty("penetrationLimitCount");
        moveType = serializedObject.FindProperty("moveType");
        colliderOffset = serializedObject.FindProperty("colliderOffset");
        colliderRadius = serializedObject.FindProperty("colliderRadius");
        shootAnimation = serializedObject.FindProperty("shootAnimation");

        // 親クラス WeaponData のプロパティ
        itemName = serializedObject.FindProperty("itemName");
        itemSprite = serializedObject.FindProperty("itemSprite");
        itemRank = serializedObject.FindProperty("itemRank");
        buyPrice = serializedObject.FindProperty("buyPrice");
        sellPrice = serializedObject.FindProperty("sellPrice");
        description = serializedObject.FindProperty("description");
        wpCost = serializedObject.FindProperty("wpCost");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // 表示順を明示的に指定
        EditorGUILayout.PropertyField(weaponID); // ← 一番上に
        EditorGUILayout.Space();

        EditorGUILayout.LabelField("【基本情報】", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(itemName, new GUIContent("表示名"));
        EditorGUILayout.PropertyField(itemSprite, new GUIContent("アイコン"));
        EditorGUILayout.PropertyField(itemRank, new GUIContent("レア度"));
        EditorGUILayout.PropertyField(buyPrice, new GUIContent("購入価格"));
        EditorGUILayout.PropertyField(sellPrice, new GUIContent("売却価格"));
        EditorGUILayout.PropertyField(description, new GUIContent("説明文"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("【射撃用データ】", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(power, new GUIContent("攻撃力"));
        EditorGUILayout.PropertyField(wpCost, new GUIContent("WP消費量"));
        EditorGUILayout.PropertyField(cooldownTime, new GUIContent("クールタイム（秒）"));
        EditorGUILayout.PropertyField(shootSpeed, new GUIContent("弾の速度"));
        EditorGUILayout.PropertyField(vanishTime, new GUIContent("消滅時間(秒)"));
        EditorGUILayout.PropertyField(shotInterval, new GUIContent("発射間隔"));
        EditorGUILayout.PropertyField(penetrationLimitCount, new GUIContent("貫通限界数"));
        EditorGUILayout.PropertyField(moveType, new GUIContent("弾の移動タイプ"));
        EditorGUILayout.PropertyField(colliderRadius, new GUIContent("Colliderの半径"));
        EditorGUILayout.PropertyField(colliderOffset, new GUIContent("Colliderの座標オフセット"));
        EditorGUILayout.PropertyField(shootAnimation, new GUIContent("アニメーション(任意)"));

        serializedObject.ApplyModifiedProperties();
    }
}

[CustomEditor(typeof(BladeWeaponData))]
public class BladeWeaponDataEditor : Editor
{
    SerializedProperty weaponID;
    SerializedProperty power;
    SerializedProperty cooldownTime;
    SerializedProperty attackTime;
    SerializedProperty colliderOffset;
    SerializedProperty colliderSize;

    // 親クラス WeaponData のプロパティ
    SerializedProperty itemName;
    SerializedProperty itemSprite;
    SerializedProperty itemRank;
    SerializedProperty buyPrice;
    SerializedProperty sellPrice;
    SerializedProperty description;
    SerializedProperty wpCost;

    void OnEnable()
    {
        // BladeWeaponData 独自
        weaponID = serializedObject.FindProperty("weaponID");
        power = serializedObject.FindProperty("power");
        cooldownTime = serializedObject.FindProperty("cooldownTime");
        attackTime = serializedObject.FindProperty("attackTime");
        colliderOffset = serializedObject.FindProperty("ColliderOffset");
        colliderSize = serializedObject.FindProperty("ColliderSize");

        // 親 WeaponData の共通項目
        itemName = serializedObject.FindProperty("itemName");
        itemSprite = serializedObject.FindProperty("itemSprite");
        itemRank = serializedObject.FindProperty("itemRank");
        buyPrice = serializedObject.FindProperty("buyPrice");
        sellPrice = serializedObject.FindProperty("sellPrice");
        description = serializedObject.FindProperty("description");
        wpCost = serializedObject.FindProperty("wpCost");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // 表示順の明示的制御
        EditorGUILayout.PropertyField(weaponID); // ← 最上部に表示
        EditorGUILayout.Space();

        EditorGUILayout.LabelField("【基本情報】", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(itemName, new GUIContent("表示名"));
        EditorGUILayout.PropertyField(itemSprite, new GUIContent("アイコン"));
        EditorGUILayout.PropertyField(itemRank, new GUIContent("レア度"));
        EditorGUILayout.PropertyField(buyPrice, new GUIContent("購入価格"));
        EditorGUILayout.PropertyField(sellPrice, new GUIContent("売却価格"));
        EditorGUILayout.PropertyField(description, new GUIContent("説明文"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("【近接武器データ】", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(power, new GUIContent("攻撃力"));
        EditorGUILayout.PropertyField(wpCost, new GUIContent("WP消費量"));
        EditorGUILayout.PropertyField(cooldownTime, new GUIContent("クールタイム（秒）"));
        EditorGUILayout.PropertyField(attackTime, new GUIContent("攻撃時間（秒）"));
        EditorGUILayout.PropertyField(colliderSize, new GUIContent("Colliderの大きさ"));
        EditorGUILayout.PropertyField(colliderOffset, new GUIContent("Colliderの座標オフセット"));

        serializedObject.ApplyModifiedProperties();
    }
}
