using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public abstract class WeaponDataEditorBase : Editor
{
    protected SerializedProperty itemName, itemSprite, itemRank, buyPrice, sellPrice;
    protected SerializedProperty isSellable, description, wpCost;
    protected bool basicOpen = true;

    protected virtual void OnEnable()
    {
        itemName = serializedObject.FindProperty("itemName");
        itemSprite = serializedObject.FindProperty("itemSprite");
        itemRank = serializedObject.FindProperty("itemRank");
        buyPrice = serializedObject.FindProperty("buyPrice");
        sellPrice = serializedObject.FindProperty("sellPrice");
        isSellable = serializedObject.FindProperty("isSellable");
        description = serializedObject.FindProperty("description");
        wpCost = serializedObject.FindProperty("wpCost");
    }

    protected void DrawBasicInfo()
    {
        DrawSection("基本情報", ref basicOpen, () =>
        {
            EditorGUILayout.PropertyField(itemName, new GUIContent("表示名"));
            EditorGUILayout.PropertyField(itemSprite, new GUIContent("アイコン"));
            EditorGUILayout.PropertyField(itemRank, new GUIContent("レア度"));
            EditorGUILayout.PropertyField(buyPrice, new GUIContent("購入価格"));
            EditorGUILayout.PropertyField(isSellable, new GUIContent("売却可能"));
            using (new EditorGUI.DisabledScope(!isSellable.hasMultipleDifferentValues && !isSellable.boolValue))
                EditorGUILayout.PropertyField(sellPrice, new GUIContent("売却価格"));
            EditorGUILayout.PropertyField(description, new GUIContent("説明文"));
        });
    }

    protected static void DrawSection(string title, ref bool open, System.Action content)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        open = EditorGUILayout.Foldout(open, title, true, EditorStyles.foldoutHeader);
        if (open)
        {
            EditorGUI.indentLevel++;
            content();
            EditorGUI.indentLevel--;
        }
        EditorGUILayout.EndVertical();
    }

    protected void DrawUnmapped(HashSet<string> mapped)
    {
        SerializedProperty iterator = serializedObject.GetIterator();
        bool enterChildren = true;
        bool hasHeader = false;
        while (iterator.NextVisible(enterChildren))
        {
            enterChildren = false;
            if (mapped.Contains(iterator.name))
                continue;
            if (!hasHeader)
            {
                EditorGUILayout.HelpBox(
                    "専用Inspectorに未登録の項目を暫定表示しています。WeaponDataEditorへの登録を確認してください。",
                    MessageType.Warning
                );
                hasHeader = true;
            }
            EditorGUILayout.PropertyField(iterator, true);
        }
    }

}

[CanEditMultipleObjects]
[CustomEditor(typeof(ShootWeaponData))]
public class ShootWeaponDataEditor : WeaponDataEditorBase
{
    private static readonly HashSet<string> Mapped = new HashSet<string>
    {
        "m_Script", "weaponID", "itemName", "itemSprite", "itemRank", "buyPrice",
        "sellPrice", "isSellable", "description", "wpCost", "power", "cooldownTime",
        "shootSpeed", "vanishTime", "shotInterval", "penetrationLimitCount", "moveType",
        "gravityScale", "upwardAngle", "colliderOffset", "colliderRadius", "shootAnimation",
        "boomerangFlyTime", "boomerangDistance", "boomerangCurveWidth", "isBoomerangOverhand",
        "boomerangMinYOffset", "boomerangRotationSpeed", "maxActiveBoomerangCount",
        "bouncingLaunchAngle", "bouncingGravityScale", "bouncingMaxCount", "bouncingHeight"
    };

    private SerializedProperty weaponID, power, cooldownTime, shootSpeed, vanishTime;
    private SerializedProperty shotInterval, penetrationLimitCount, moveType, shootAnimation;
    private SerializedProperty colliderOffset, colliderRadius, gravityScale, upwardAngle;
    private SerializedProperty boomerangFlyTime, boomerangDistance, boomerangCurveWidth;
    private SerializedProperty isBoomerangOverhand, boomerangMinYOffset, boomerangRotationSpeed;
    private SerializedProperty maxActiveBoomerangCount, bouncingLaunchAngle;
    private SerializedProperty bouncingGravityScale, bouncingMaxCount, bouncingHeight;
    private bool attackOpen = true, projectileOpen = true, trajectoryOpen = true, colliderOpen = true;

    protected override void OnEnable()
    {
        base.OnEnable();
        weaponID = Find("weaponID"); power = Find("power"); cooldownTime = Find("cooldownTime");
        shootSpeed = Find("shootSpeed"); vanishTime = Find("vanishTime");
        shotInterval = Find("shotInterval"); penetrationLimitCount = Find("penetrationLimitCount");
        moveType = Find("moveType"); shootAnimation = Find("shootAnimation");
        colliderOffset = Find("colliderOffset"); colliderRadius = Find("colliderRadius");
        gravityScale = Find("gravityScale"); upwardAngle = Find("upwardAngle");
        boomerangFlyTime = Find("boomerangFlyTime"); boomerangDistance = Find("boomerangDistance");
        boomerangCurveWidth = Find("boomerangCurveWidth");
        isBoomerangOverhand = Find("isBoomerangOverhand");
        boomerangMinYOffset = Find("boomerangMinYOffset");
        boomerangRotationSpeed = Find("boomerangRotationSpeed");
        maxActiveBoomerangCount = Find("maxActiveBoomerangCount");
        bouncingLaunchAngle = Find("bouncingLaunchAngle");
        bouncingGravityScale = Find("bouncingGravityScale");
        bouncingMaxCount = Find("bouncingMaxCount"); bouncingHeight = Find("bouncingHeight");
    }

    private SerializedProperty Find(string name) => serializedObject.FindProperty(name);

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.PropertyField(weaponID, new GUIContent("射撃武器ID"));
        EditorGUILayout.Space();
        DrawBasicInfo();
        DrawSection("攻撃性能", ref attackOpen, DrawAttack);
        DrawSection("弾の基本設定", ref projectileOpen, DrawProjectile);
        DrawSection("移動タイプ別設定", ref trajectoryOpen, DrawTrajectory);
        DrawSection("当たり判定", ref colliderOpen, () =>
        {
            EditorGUILayout.PropertyField(colliderRadius, new GUIContent("Collider半径"));
            EditorGUILayout.PropertyField(colliderOffset, new GUIContent("Colliderオフセット"));
        });
        DrawWarnings();
        DrawUnmapped(Mapped);
        serializedObject.ApplyModifiedProperties();
    }

    private void DrawAttack()
    {
        EditorGUILayout.PropertyField(power, new GUIContent("攻撃力"));
        EditorGUILayout.PropertyField(wpCost, new GUIContent("WP消費量"));
        EditorGUILayout.PropertyField(cooldownTime, new GUIContent("同一対象への再命中間隔（秒）"));
        EditorGUILayout.PropertyField(shotInterval, new GUIContent("発射間隔（秒）"));
        EditorGUILayout.PropertyField(penetrationLimitCount, new GUIContent("貫通限界数"));
    }

    private void DrawProjectile()
    {
        EditorGUILayout.PropertyField(moveType, new GUIContent("移動タイプ"));
        EditorGUILayout.PropertyField(shootSpeed, new GUIContent("弾速"));
        EditorGUILayout.PropertyField(vanishTime, new GUIContent("生存時間（秒）"));
        EditorGUILayout.PropertyField(shootAnimation, new GUIContent("アニメーション（任意）"));
    }

    private void DrawTrajectory()
    {
        if (moveType.hasMultipleDifferentValues)
        {
            EditorGUILayout.HelpBox("移動タイプが混在しています。固有設定は個別選択時に表示されます。", MessageType.Info);
            return;
        }
        switch ((ShootWeaponData.ShootMoveType)moveType.intValue)
        {
            case ShootWeaponData.ShootMoveType.None:
                EditorGUILayout.HelpBox("移動タイプが設定されていません。", MessageType.Warning);
                break;
            case ShootWeaponData.ShootMoveType.Straight:
                EditorGUILayout.HelpBox("弾速に従って直進します。", MessageType.Info);
                break;
            case ShootWeaponData.ShootMoveType.Parallel3Way:
                EditorGUILayout.HelpBox("中央・上・下の3方向へ発射します。", MessageType.Info);
                break;
            case ShootWeaponData.ShootMoveType.Parabola:
                Field(gravityScale, "重力スケール"); Field(upwardAngle, "上昇角度");
                break;
            case ShootWeaponData.ShootMoveType.Boomerang:
                Field(boomerangFlyTime, "往復時間（秒）"); Field(boomerangDistance, "飛距離");
                Field(boomerangCurveWidth, "軌道の上下幅"); Field(isBoomerangOverhand, "上回り");
                Field(boomerangMinYOffset, "最低高度オフセット");
                Field(boomerangRotationSpeed, "回転速度（度/秒）");
                Field(maxActiveBoomerangCount, "最大同時発射数");
                break;
            case ShootWeaponData.ShootMoveType.Bouncing:
                Field(bouncingLaunchAngle, "発射角度"); Field(bouncingGravityScale, "重力スケール");
                Field(bouncingMaxCount, "最大バウンド数"); Field(bouncingHeight, "バウンド高さ");
                break;
        }
    }

    private static void Field(SerializedProperty property, string label) =>
        EditorGUILayout.PropertyField(property, new GUIContent(label));

    private void DrawWarnings()
    {
        if (serializedObject.isEditingMultipleObjects) return;
        if (vanishTime.floatValue <= 0f) Warn("生存時間は0より大きい値を設定してください。");
        if (penetrationLimitCount.intValue <= 0) Warn("貫通限界数が0以下のため、最初の命中で消滅します。");
        if (moveType.intValue == (int)ShootWeaponData.ShootMoveType.Boomerang && maxActiveBoomerangCount.intValue <= 0)
            Warn("ブーメランの最大同時発射数は1以上にしてください。");
        if (moveType.intValue == (int)ShootWeaponData.ShootMoveType.Bouncing && bouncingGravityScale.floatValue <= 0f)
            Warn("重力が0以下の場合、バウンド地点へ落下しません。");
    }

    private static void Warn(string message) => EditorGUILayout.HelpBox(message, MessageType.Warning);
}

[CanEditMultipleObjects]
[CustomEditor(typeof(BladeWeaponData))]
public class BladeWeaponDataEditor : WeaponDataEditorBase
{
    private static readonly HashSet<string> Mapped = new HashSet<string>
    {
        "m_Script", "weaponID", "itemName", "itemSprite", "itemRank", "buyPrice", "sellPrice",
        "isSellable", "description", "wpCost", "attackActionData", "power", "cooldownTime",
        "ColliderOffset", "ColliderSize"
    };
    private SerializedProperty weaponID, attackActionData, power, cooldownTime, colliderOffset, colliderSize;
    private bool attackOpen = true, motionOpen = true, colliderOpen = true;

    protected override void OnEnable()
    {
        base.OnEnable();
        weaponID = serializedObject.FindProperty("weaponID");
        attackActionData = serializedObject.FindProperty("attackActionData");
        power = serializedObject.FindProperty("power");
        cooldownTime = serializedObject.FindProperty("cooldownTime");
        colliderOffset = serializedObject.FindProperty("ColliderOffset");
        colliderSize = serializedObject.FindProperty("ColliderSize");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.PropertyField(weaponID, new GUIContent("近接武器ID"));
        EditorGUILayout.Space();
        DrawBasicInfo();
        DrawSection("攻撃性能", ref attackOpen, () =>
        {
            EditorGUILayout.PropertyField(power, new GUIContent("攻撃力"));
            EditorGUILayout.PropertyField(wpCost, new GUIContent("WP消費量"));
            EditorGUILayout.PropertyField(cooldownTime, new GUIContent("同一対象への再命中間隔（秒）"));
        });
        DrawSection("攻撃モーション", ref motionOpen, () =>
            EditorGUILayout.PropertyField(attackActionData, new GUIContent("攻撃モーションデータ")));
        DrawSection("当たり判定", ref colliderOpen, () =>
        {
            EditorGUILayout.PropertyField(colliderSize, new GUIContent("Colliderサイズ"));
            EditorGUILayout.PropertyField(colliderOffset, new GUIContent("Colliderオフセット"));
        });
        if (!serializedObject.isEditingMultipleObjects)
        {
            if (attackActionData.objectReferenceValue == null)
                EditorGUILayout.HelpBox("攻撃モーションデータが設定されていません。", MessageType.Warning);
            Vector2 size = colliderSize.vector2Value;
            if (size.x <= 0f || size.y <= 0f)
                EditorGUILayout.HelpBox("ColliderサイズはX・Yともに0より大きい値が必要です。", MessageType.Warning);
        }
        DrawUnmapped(Mapped);
        serializedObject.ApplyModifiedProperties();
    }
}
