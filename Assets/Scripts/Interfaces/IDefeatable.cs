/// <summary>
/// 「倒される」という状態を持つオブジェクトが実装するインターフェース。
/// </summary>
public interface IDefeatable
{
    /// <summary>
    /// このオブジェクトが倒された状態かどうかを取得します。
    /// </summary>
    bool IsDefeated { get; }
}