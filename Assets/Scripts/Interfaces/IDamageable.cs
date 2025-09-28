/// <summary>
/// ダメージを受ける可能性のあるオブジェクトが実装するインターフェース。
/// 最大HPと現在HPを管理し、ダメージを処理する機能を提供します。
/// </summary>
public interface IDamageable
{
    /// <summary>
    /// オブジェクトの最大ヒットポイント (HP) を取得します。
    /// この値は通常、オブジェクトの初期設定時に決定され、ゲーム中に変化することはありません。
    /// </summary>
    int MaxHP { get; }

    /// <summary>
    /// オブジェクトの現在のヒットポイント (HP) を取得します。
    /// この値はダメージを受けることで減少し、回復することで増加します。
    /// </summary>
    int CurrentHP { get; }

    /// <summary>
    /// 指定された量のダメージをオブジェクトに適用します。
    /// このメソッドが呼び出されると、CurrentHPが減少し、
    /// HPがゼロになった場合のロジック（例：オブジェクトの破壊など）が実行されます。
    /// </summary>
    /// <param name="damage">オブジェクトに与えるダメージ量。</param>
    void Damage(int damage);
}