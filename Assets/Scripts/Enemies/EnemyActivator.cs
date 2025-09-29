using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// レアな敵の出現情報（ゲームオブジェクトと出現確率）を格納するクラス
/// </summary>
[System.Serializable] // この属性により、インスペクター上でリストの要素として編集可能になります
public class RareEnemyInfo
{
    [Tooltip("確率で出現させたいレア敵のゲームオブジェクト")]
    public GameObject enemyObject;

    [Tooltip("このレア敵が出現する確率（パーセント）")]
    [Range(0f, 100f)]
    public float spawnChance = 10.0f; // デフォルトの出現確率を10%に設定
}

/// <summary>
/// CameraMoveAreaと連携し、特定のエリアに入ったときに子オブジェクト（敵など）を有効化/無効化する
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class EnemyActivator : MonoBehaviour
{
    [SerializeField]
    [Tooltip("このEnemyActivatorを起動させるCameraMoveArea")]
    private CameraMoveArea targetCameraArea;

    [Header("レア敵の設定")]
    [Tooltip(
        "確率で出現するレア敵をここに登録します。リストに登録されていない子は、通常通り毎回出現します。"
    )]
    [SerializeField]
    private List<RareEnemyInfo> rareEnemies;
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
    /// <param name="isActive">有効にする場合はtrue、無効にする場合はfalse</param>
    private void SetChildrenActive(bool isActive)
    {
        // === プレイヤーがエリアに入った時の有効化処理 ===
        if (isActive)
        {
            // 高速で参照するために、レア敵のGameObjectをHashSetに格納します。
            var rareEnemySet = new HashSet<GameObject>();
            if (rareEnemies != null)
            {
                foreach (var rareInfo in rareEnemies)
                {
                    if (rareInfo.enemyObject != null)
                    {
                        rareEnemySet.Add(rareInfo.enemyObject);
                    }
                }
            }

            // まず、通常の子オブジェクト（レア敵リストにないもの）を有効化します。
            foreach (Transform child in transform)
            {
                if (child == null)
                    continue;

                // この子がレア敵セットに含まれていなければ、通常敵として扱う
                if (!rareEnemySet.Contains(child.gameObject))
                {
                    child.gameObject.SetActive(true);
                    ResetChildState(child);
                }
            }

            // 次に、レア敵の出現判定を行います。
            if (rareEnemies != null)
            {
                foreach (var rareInfo in rareEnemies)
                {
                    if (rareInfo.enemyObject == null)
                        continue;

                    // 0から100までの乱数を生成し、出現確率と比較
                    bool shouldSpawn = Random.Range(0f, 100f) <= rareInfo.spawnChance;

                    // 判定結果に応じてオブジェクトを有効化/無効化
                    rareInfo.enemyObject.SetActive(shouldSpawn);

                    // もし出現させるなら、状態をリセット
                    if (shouldSpawn)
                    {
                        ResetChildState(rareInfo.enemyObject.transform);
                    }
                }
            }
        }
        // === プレイヤーがエリアから出た時の無効化処理 ===
        else
        {
            foreach (Transform child in transform)
            {
                if (child == null)
                    continue;

                // ドロップアイテムなどが残っていれば削除（元のロジックを継承）
                // 安全のため、非アクティブ化する前にコンポーネントを検索します。
                DropItem dropItem = child.GetComponent<DropItem>();
                if (dropItem != null)
                {
                    Destroy(dropItem.gameObject);
                }

                // 最後に子オブジェクト自体を非アクティブ化
                child.gameObject.SetActive(false);
            }
        }
    }

    /// <summary>
    /// 子オブジェクトが持つIEnemyResettableインターフェースを呼び出し、状態をリセットします。
    /// </summary>
    private void ResetChildState(Transform child)
    {
        // 子オブジェクトにアタッチされているすべてのIEnemyResettableコンポーネントを取得
        IEnemyResettable[] resettables = child.GetComponents<IEnemyResettable>();

        if (resettables.Length > 0)
        {
            foreach (IEnemyResettable resettable in resettables)
            {
                // 各コンポーネントのResetStateメソッドを呼び出す
                resettable.ResetState(); // HPなどの初期化
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
