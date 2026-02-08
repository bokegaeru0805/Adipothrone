using UnityEngine;

/// <summary>
/// SpriteRenderer の size (Width / Height) を外部（UnityEvent等）から
/// 制御するためのブリッジ（橋渡し）クラスです。
/// </summary>
/// <remarks>
/// UnityEvent は Vector2 などの構造体を直接編集できない制限があるため、
/// このクラスを経由して float 値で操作できるようにします。
/// </remarks>
public class SpriteSizeController : MonoBehaviour
{
    [Header("参照設定")]
    [SerializeField, Tooltip("サイズを操作する対象の SpriteRenderer")]
    private SpriteRenderer _spriteRenderer;

    private void Awake()
    {
        // インスペクターで未設定の場合は自オブジェクトから取得を試みる
        if (_spriteRenderer == null)
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
        }
    }

    /// <summary>
    /// Sprite の幅 (Width) のみを変更します。
    /// </summary>
    /// <param name="width">新しい幅の数値</param>
    public void SetWidth(float width)
    {
        if (_spriteRenderer == null) return;

        // Vector2 は構造体であるため、一度変数に取り出して再代入する必要があります
        Vector2 currentSize = _spriteRenderer.size;
        _spriteRenderer.size = new Vector2(width, currentSize.y);
    }

    /// <summary>
    /// Sprite の高さ (Height) のみを変更します。
    /// </summary>
    /// <param name="height">新しい高さの数値</param>
    public void SetHeight(float height)
    {
        if (_spriteRenderer == null) return;

        Vector2 currentSize = _spriteRenderer.size;
        _spriteRenderer.size = new Vector2(currentSize.x, height);
    }

    /// <summary>
    /// Sprite の幅と高さを同時に変更します。
    /// </summary>
    /// <param name="width">新しい幅</param>
    /// <param name="height">新しい高さ</param>
    public void SetSize(float width, float height)
    {
        if (_spriteRenderer == null) return;

        _spriteRenderer.size = new Vector2(width, height);
    }

    /// <summary>
    /// Sprite のサイズを比率（倍率）で変更します。
    /// </summary>
    /// <param name="multiplier">現在のサイズに乗算する値</param>
    public void MultiplySize(float multiplier)
    {
        if (_spriteRenderer == null) return;

        _spriteRenderer.size *= multiplier;
    }
}