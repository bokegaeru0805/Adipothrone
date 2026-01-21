using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// ロード時にプレイヤーがこのエリア内にいた場合、指定した安全な座標へ強制移動させるクラス。
/// 縦穴落下中や地形ハマり防止に使用します。
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class PlayerSpawnCorrectorArea : MonoBehaviour
{
    #region Static Management

    // シーン内の全コレクターを管理する静的リスト
    public static List<PlayerSpawnCorrectorArea> ActiveInstances { get; private set; } =
        new List<PlayerSpawnCorrectorArea>();

    #endregion

    #region Inspector Settings

    [Header("補正設定")]
    [Tooltip("このエリア内でロードされた場合に移動させる、安全な場所の座標(World Coordinate)")]
    [SerializeField]
    private Vector2 safeSpawnPosition;

    #endregion

    #region Internal State

    private Collider2D checkZone;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        checkZone = GetComponent<Collider2D>();

        // 判定にはTriggerを使用することを推奨（物理干渉を防ぐため）
        if (!checkZone.isTrigger)
        {
            checkZone.isTrigger = true;
            Debug.LogWarning($"{name} のColliderをTriggerモードに変更しました。", this);
        }

        // リストに登録
        ActiveInstances.Add(this);
    }

    private void OnDestroy()
    {
        // リストから解除
        ActiveInstances.Remove(this);
    }

    /// <summary>
    /// エディタ上でコンポーネントを追加した際などに、初期値を現在の位置に設定する
    /// </summary>
    private void Reset()
    {
        safeSpawnPosition = transform.position;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// 指定された座標が、このコレクターのエリア内に含まれているかを判定します。
    /// </summary>
    public bool IsPositionInArea(Vector2 position)
    {
        if (checkZone == null)
            return false;
        return checkZone.OverlapPoint(position);
    }

    /// <summary>
    /// 設定された安全な座標を返します。
    /// </summary>
    public Vector2 GetSafeSpawnPosition()
    {
        // 指定されたVector2座標をそのまま返す
        // (0,0)の場合でも、それが意図された座標であればそのまま返します
        return safeSpawnPosition;
    }

    #endregion

    #region Editor Visualization

    private void OnDrawGizmos()
    {
        // --- 色の設定 ---
        // 既存の赤、グレー、マゼンタ、シアン、緑、オレンジと被らない「黄色」を採用
        Color fillColor = new Color(1f, 0.92f, 0.016f, 0.2f); // 半透明の黄色
        Color borderColor = Color.yellow; // 黄色

        Collider2D col = GetComponent<Collider2D>();
        if (col == null) return;

        // Gizmoの中心位置計算
        Vector3 centerPos = col.bounds.center;

        // エリアの描画
        Gizmos.color = fillColor;
        Gizmos.DrawCube(centerPos, col.bounds.size);

        Gizmos.color = borderColor;
        Gizmos.DrawWireCube(centerPos, col.bounds.size);

        // 移動先の描画（指定座標へ線を引く）
        // Vector2をVector3にキャストして描画に使用
        Vector3 targetPos = (Vector3)safeSpawnPosition;

        Gizmos.color = Color.white; // 線は見やすく白に近い色などで補足
        Gizmos.DrawLine(centerPos, targetPos);

        Gizmos.color = borderColor;
        Gizmos.DrawWireSphere(targetPos, 0.5f); // 目標地点に球体を表示

        // --- 文字ラベルの表示 ---
#if UNITY_EDITOR
        // オブジェクト名から表示する文字列を作成
        string labelText = gameObject.name;
        string[] splitName = labelText.Split('_');
        if (splitName.Length > 1)
        {
            labelText = splitName[splitName.Length - 1];
        }

        GUIStyle style = new GUIStyle();
        style.normal.textColor = Color.yellow; // ラベルも黄色系で統一
        style.alignment = TextAnchor.MiddleCenter;
        style.fontSize = 12;
        style.fontStyle = FontStyle.Bold;

        Handles.Label(centerPos, labelText, style);
#endif
    }

    #endregion
}