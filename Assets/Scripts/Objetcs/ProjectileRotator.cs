using UnityEngine;

/// <summary>
/// 投擲物や回転オブジェクトの自転を制御するコンポーネント。
/// </summary>
public class ProjectileRotator : MonoBehaviour
{
    [Header("回転設定")]
    [SerializeField]
    [Tooltip("1秒あたりの回転角度（正の値で反時計回り、負の値で時計回り）")]
    private float _rotateSpeed = -720f;

    [SerializeField]
    [Tooltip("Updateで回転させるか、FixedUpdateで回転させるか")]
    private bool _useFixedUpdate = false;

    private void Update()
    {
        if (!_useFixedUpdate)
        {
            Rotate();
        }
    }

    private void FixedUpdate()
    {
        if (_useFixedUpdate)
        {
            Rotate();
        }
    }

    /// <summary>
    /// オブジェクトをZ軸中心に回転させます。
    /// </summary>
    private void Rotate()
    {
        float delta = _useFixedUpdate ? Time.fixedDeltaTime : Time.deltaTime;
        transform.Rotate(0, 0, _rotateSpeed * delta);
    }

    /// <summary>
    /// 外部から回転速度を変更する場合に使用します。
    /// </summary>
    /// <param name="speed">新しい回転速度</param>
    public void SetRotateSpeed(float speed)
    {
        _rotateSpeed = speed;
    }
}
