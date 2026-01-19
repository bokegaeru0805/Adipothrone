using UnityEngine;

/// <summary>
/// ノックバックの計算タイプ
/// </summary>
/// <remarks>
/// - HorizontalFromSource: 接触点（敵の中心）から、プレイヤーへのX軸方向（真横）へ飛ぶ
/// - RadialFromSource: 接触点からプレイヤーへのベクトル方向（全方位）へ飛ぶ
/// - FixedVector: 敵の位置に関係なく、指定された固定ベクトル方向へ飛ぶ
/// </remarks>
public enum KnockbackType
{
    HorizontalFromSource, // 接触点（敵の中心）から、プレイヤーへのX軸方向（真横）へ飛ぶ
    RadialFromSource,     // 接触点からプレイヤーへのベクトル方向（全方位）へ飛ぶ
    FixedVector           // 敵の位置に関係なく、指定された固定ベクトル方向へ飛ぶ
}

/// <summary>
/// ダメージ発生源からプレイヤーへ渡されるノックバック情報
/// </summary>
[System.Serializable]
public struct KnockbackData
{
    public KnockbackType type;
    public Vector2 sourcePosition; // ダメージ発生源の座標
    public Vector2 fixedDirection; // 固定ベクトルの場合の方向
    public float force;            // ノックバックの強さ（0ならノックバックなし）
}