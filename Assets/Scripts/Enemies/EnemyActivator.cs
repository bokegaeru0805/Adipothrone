using UnityEngine;

/// <summary>
/// CameraMoveAreaと連携し、特定のエリアに入ったときに子オブジェクト（敵など）を有効化/無効化する
/// </summary>
[RequireComponent(typeof(BoxCollider2D))] // このスクリプトにはBoxCollider2Dが必須であることを示す
public class EnemyActivator : MonoBehaviour
{
    [SerializeField]
    [Tooltip("このEnemyActivatorを起動させるCameraMoveArea")]
    private CameraMoveArea targetCameraArea;
    private BoxCollider2D activationZone;

    private void Awake()
    {
        if (targetCameraArea == null)
        {
            Debug.LogError($"{name} に targetCameraArea が設定されていません。", this);
        }

        // 自身のColliderを取得し、必ずTriggerに設定されているか確認
        activationZone = GetComponent<BoxCollider2D>();
        if (!activationZone.isTrigger)
        {
            activationZone.isTrigger = true;
            Debug.LogWarning(
                $"{name} のBoxCollider2Dで 'Is Trigger' が有効でなかったため、自動で設定しました。",
                this
            );
        }
    }

    private void Start()
    {
        // ゲーム開始時は、管理下のオブジェクトを全て非表示にする
        SetChildrenActive(false);
    }

    private void OnEnable()
    {
        CameraMoveArea.OnPlayerEnteredArea += HandlePlayerEnteredArea;
        CameraMoveArea.OnPlayerExitedArea += HandlePlayerExitedArea;
    }

    private void OnDisable()
    {
        CameraMoveArea.OnPlayerEnteredArea -= HandlePlayerEnteredArea;
        CameraMoveArea.OnPlayerExitedArea -= HandlePlayerExitedArea;
    }

    private void HandlePlayerEnteredArea(CameraMoveArea enteredArea)
    {
        // プレイヤーが入ったエリアが、自分が監視しているエリアなら子を有効化
        if (enteredArea == targetCameraArea)
        {
            SetChildrenActive(true);
        }
    }

    private void HandlePlayerExitedArea(CameraMoveArea exitedArea)
    {
        // プレイヤーが出たエリアが、自分が監視しているエリアなら子を無効化
        if (exitedArea == targetCameraArea)
        {
            SetChildrenActive(false);
        }
    }

    /// <summary>
    /// 子オブジェクトのアクティブ状態を設定します。
    /// /// isActive が true の場合、子オブジェクトの状態をリセットします。
    /// </summary>
    private void SetChildrenActive(bool isActive)
    {
        // 子オブジェクトのリストをループ処理
        foreach (Transform child in transform)
        {
            // 子オブジェクトがnullの場合はスキップ
            if (child == null)
                continue;

            // まず、子オブジェクトのアクティブ状態を切り替える
            child.gameObject.SetActive(isActive);

            // もしisActiveがtrueの場合、状態リセット処理を実行
            if (isActive)
            {
                // 子オブジェクトにアタッチされているすべてのIEnemyResettableコンポーネントを取得
                // GetComponents<T>() は、指定した型のコンポーネントをすべて配列で返す
                IEnemyResettable[] resettables = child.GetComponents<IEnemyResettable>();

                // 取得したすべてのResettableコンポーネントをループ処理
                if (resettables.Length > 0)
                {
                    foreach (IEnemyResettable resettable in resettables)
                    {
                        // 各コンポーネントのResetStateメソッドを呼び出す
                        resettable.ResetState(); // HPなどの初期化
                    }
                }
            }
            else // isActiveがfalseの場合
            {
                // 子オブジェクトにアタッチされているDropItemコンポーネントを取得
                DropItem dropItem = child.GetComponent<DropItem>();
                if (dropItem != null)
                {
                    // DropItemコンポーネントがあれば、そのゲームオブジェクトを削除
                    // これにより、倒された敵が落としたアイテムなどが消える
                    Destroy(dropItem.gameObject);
                }
            }
        }
    }

    private void OnDrawGizmos()
    {
        // Awake前（編集中）でも動作するように、nullなら取得を試みる
        if (activationZone == null)
        {
            activationZone = GetComponent<BoxCollider2D>();
        }

        // Gizmoの色を設定
        Color fillColor = new Color(0f, 1f, 0f, 0.2f); // 半透明の緑
        Color borderColor = Color.green;

        // BoxCollider2Dの範囲情報を使ってGizmoを描画
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = fillColor;
        Gizmos.DrawCube(activationZone.offset, activationZone.size);
        Gizmos.color = borderColor;
        Gizmos.DrawWireCube(activationZone.offset, activationZone.size);
    }
}
