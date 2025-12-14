using NaughtyAttributes;
using UnityEngine;

/// <summary>
/// 開発・デバッグ用の簡易追尾カメラ。
/// プレイヤーとのオフセット（距離・位置関係）を維持して追尾します。
/// </summary>
public class DebugCameraFollow : MonoBehaviour
{
#if UNITY_EDITOR
    [Tooltip("カメラ追従を行うかどうか")]
    [SerializeField]
    private bool isEnableFollow = true;

    [Tooltip("手動でオフセットを設定する場合の値")]
    [SerializeField, ShowIf(nameof(isEnableFollow))]
    private Vector2 manualOffset;

    [Header("Follow Settings")]
    [Tooltip("追尾の滑らかさ（0に近いほど遅く、1で瞬時に追尾）")]
    [SerializeField, Range(0.01f, 1f), ShowIf(nameof(isEnableFollow))]
    private float smoothSpeed = 1f;

    // 内部で使用するオフセット値
    private Vector2 _currentOffset;
    private Transform target;

    private void Start()
    {
        if (!isEnableFollow)
            this.enabled = false;

        // ターゲットが未設定の場合、"Player"タグのついたオブジェクトを自動検索する（デバッグ用機能）
        if (target == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag(GameConstants.PlayerTagName);
            if (playerObj != null)
            {
                target = playerObj.transform;
            }
            else
            {
                Debug.LogWarning(
                    "[DebugCameraFollow] ターゲットが設定されておらず、Playerタグも見つかりません。"
                );
            }
        }

        // オフセットの初期化
        if (target != null)
        {
            // 手動設定値を使用
            _currentOffset = manualOffset;

            var playerController = target.GetComponent<PlayerTestMoveController>();
            if (playerController != null && isEnableFollow)
            {
                playerController.useKeyboardInput = true; // プレイヤーのキーボード操作を無効化
            }
        }
    }

    // カメラの追尾は LateUpdate で行うのが定石（プレイヤーの移動処理が終わった後にカメラを動かすため）
    void LateUpdate()
    {
        if (target == null)
            return;

        // 目標位置を計算（ターゲット位置 + オフセット）
        Vector2 desiredPosition = (Vector2)target.position + _currentOffset;

        // 現在地から目標位置へ滑らかに補間移動（Lerp）
        // 瞬時に移動させたい場合は transform.position = desiredPosition; に書き換えてください
        Vector2 smoothedPosition = Vector2.Lerp(transform.position, desiredPosition, smoothSpeed);

        transform.position = new Vector3(
            smoothedPosition.x,
            smoothedPosition.y,
            transform.position.z
        );
    }
#endif
}
