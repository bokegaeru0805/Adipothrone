using System.Collections.Generic;
using NaughtyAttributes;
using UnityEngine;

/// <summary>
/// レアな敵の出現情報（ゲームオブジェクトと出現確率）を格納するクラス
/// </summary>
[System.Serializable]
public class RareEnemyInfo
{
    [Label("対象オブジェクト")]
    [Tooltip("確率で出現させたいレア敵のゲームオブジェクト")]
    public GameObject enemyObject;

    [Label("出現確率（%）")]
    [Tooltip("このレア敵が出現する確率（パーセント）")]
    [Range(0f, 100f)]
    public float spawnChance = 10.0f;
}

/// <summary>
/// フラグ条件に応じて出現させるゲームオブジェクトのグループ。
/// 同一グループ内の条件はすべて満たす必要があります。
/// </summary>
[System.Serializable]
public class ConditionalEnemyGroup
{
    [InfoBox(
        "条件成立時、確定出現リストは必ず有効化され、確率出現リストはエリア進入ごとに抽選されます。"
    )]
    [AllowNesting, Label("出現条件（すべてAND）")]
    [Tooltip("このグループを出現させるためのフラグ条件（AND条件）")]
    public List<FlagConditionPro> requiredFlags = new List<FlagConditionPro>();

    [AllowNesting, Label("条件成立時に必ず出現")]
    [Tooltip("条件を満たしたときに出現させるゲームオブジェクト")]
    public List<GameObject> enemyObjects = new List<GameObject>();

    [AllowNesting, Label("条件成立時に確率抽選")]
    [Tooltip("条件を満たした場合に、エリア進入ごとに出現確率を抽選するゲームオブジェクト")]
    public List<RareEnemyInfo> rareEnemies = new List<RareEnemyInfo>();

    /// <summary>
    /// 設定されたフラグ条件をすべて満たしているか確認します。
    /// </summary>
    public bool AreAllFlagsMet()
    {
        if (requiredFlags == null)
            return true;

        foreach (FlagConditionPro requiredFlag in requiredFlags)
        {
            if (requiredFlag == null || !requiredFlag.IsMet())
                return false;
        }

        return true;
    }
}

/// <summary>
/// 元の親（EnemyActivator）を記憶し、強制的に親元へ戻る機能を持つコンポーネント。
/// EnemyActivatorによって動的に追加されます。
/// リフトなどで親子関係が変わった場合でも、エリア管理下に復帰させるために使用します。
/// </summary>
public class EnemyParentTracker : MonoBehaviour
{
    // 本来所属すべき親オブジェクト（EnemyActivator）
    private Transform originalParent;

    /// <summary>
    /// 本来の親を設定し、追跡を開始します。
    /// </summary>
    /// <param name="parent">EnemyActivatorのTransform</param>
    public void Initialize(Transform parent)
    {
        originalParent = parent;
    }

    /// <summary>
    /// 現在の親が本来の親と異なる場合、強制的に本来の親の子要素に戻します。
    /// </summary>
    public void ReturnToOriginalParent()
    {
        // 親がnull（ルート）になっているか、別のオブジェクトの子になっている場合に実行
        if (originalParent != null && transform.parent != originalParent)
        {
            transform.SetParent(originalParent);
        }
    }
}

/// <summary>
/// CameraMoveAreaと連携し、特定のエリアに入ったときに子オブジェクト（敵など）を有効化/無効化するクラス。
/// エリア切り替え時に敵の状態をリセットする機能や、レア敵の出現管理も行います。
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(BoxCollider2D))]
public class EnemyActivator : MonoBehaviour
{
    #region インスペクター設定

    [Header("Editor識別")]
    [SerializeField]
    [Tooltip("Enemy Activator Manager上で使用する任意の表示名。未入力の場合はGameObject名を表示します。")]
    private string editorDisplayName;

    [Header("連携設定")]
    [SerializeField]
    [Tooltip("このEnemyActivatorを起動させるCameraMoveArea")]
    private CameraMoveArea targetCameraArea;

    [Tooltip(
        "エディタ上で、対象のCameraMoveAreaのBoxCollider2Dに自身のコライダーサイズとオフセットを自動追従させるかどうか"
    )]
    [SerializeField]
    private bool syncColliderWithArea = false;

    [Tooltip("自動追従時に上下左右の端からのオフセットを加算するかどうか")]
    [SerializeField, ShowIf(nameof(syncColliderWithArea))]
    private bool applyCustomOffset = false;

    [Tooltip("上端のオフセット（正の値で上へ、負の値で下へ移動）")]
    [SerializeField, ShowIf(nameof(ShouldShowOffsets))]
    private float offsetTop = 0f;

    [Tooltip("下端のオフセット（正の値で下へ、負の値で上へ移動）")]
    [SerializeField, ShowIf(nameof(ShouldShowOffsets))]
    private float offsetBottom = 0f;

    [Tooltip("左端のオフセット（正の値で右へ、負の値で左へ移動）")]
    [SerializeField, ShowIf(nameof(ShouldShowOffsets))]
    private float offsetLeft = 0f;

    [Tooltip("右端のオフセット（正の値で左へ、負の値で右へ移動）")]
    [SerializeField, ShowIf(nameof(ShouldShowOffsets))]
    private float offsetRight = 0f;

    /// <summary>
    /// NaughtyAttributes用の表示条件メソッド
    /// </summary>
    private bool ShouldShowOffsets()
    {
        return syncColliderWithArea && applyCustomOffset;
    }

    [Header("レア敵の設定")]
    [Tooltip(
        "確率で出現するレア敵をここに登録します。リストに登録されていない子は、通常通り毎回出現します。"
    )]
    [SerializeField]
    private List<RareEnemyInfo> rareEnemies;

    [Header("追加の管理対象")]
    [Tooltip(
        "子オブジェクトではないものの、このEnemyActivatorによる有効化・無効化の対象にしたいゲームオブジェクト"
    )]
    [SerializeField]
    private List<GameObject> additionalManagedObjects;

    [Header("フラグ条件付きの管理対象")]
    [InfoBox(
        "各グループのフラグ条件はAND評価です。同じ対象が複数グループにある場合、成立したグループが1つでもあれば出現候補になります。"
    )]
    [Tooltip(
        "フラグ条件をすべて満たしたときだけ出現させるグループ。確率対象は条件成立後に抽選され、フラグ変更は次回のエリア進入時に反映されます。"
    )]
    [SerializeField]
    private List<ConditionalEnemyGroup> conditionalEnemyGroups;

    #endregion

    #region 内部状態

    // このエリアの範囲を示すコライダー
    private BoxCollider2D activationZone;

    // 管理下にあるすべての子オブジェクト（のTracker）を保持するリスト
    // 親子関係が外れても参照を維持するために使用します
    private List<EnemyParentTracker> managedTrackers = new List<EnemyParentTracker>();

    #endregion

    #region Unityライフサイクル

    private void Awake()
    {
        if (targetCameraArea == null)
        {
            Debug.LogError($"{name} に targetCameraArea が設定されていません。", this);
            return;
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
        // ゲーム実行時のみトラッカーの追加や初期化を行うように修正
        if (Application.isPlaying)
        {
            // 初期化時に、現在の子オブジェクト全てにTrackerを追加して記録する
            InitializeTrackers();

            // ゲーム開始時は、管理下のオブジェクトを全て非表示にする
            SetChildrenActive(false);
        }
    }

    private void OnEnable()
    {
        // CameraMoveAreaのイベントを購読
        CameraMoveArea.OnPlayerEnteredArea += HandlePlayerEnteredArea;
        CameraMoveArea.OnPlayerExitedArea += HandlePlayerExitedArea;
    }

    private void OnDisable()
    {
        // イベント購読の解除
        CameraMoveArea.OnPlayerEnteredArea -= HandlePlayerEnteredArea;
        CameraMoveArea.OnPlayerExitedArea -= HandlePlayerExitedArea;
    }

    private void Update()
    {
#if UNITY_EDITOR
        // エディタ上かつゲームが再生されていない場合、常にコライダーを同期する
        if (!Application.isPlaying)
        {
            SyncCollider();
        }
#endif
    }

    #endregion

    #region イベントハンドラ

    /// <summary>
    /// プレイヤーがいずれかのエリアに入った時の処理
    /// </summary>
    private void HandlePlayerEnteredArea(CameraMoveArea enteredArea)
    {
        // プレイヤーが入ったエリアが、自分が監視しているエリアなら子を有効化
        if (enteredArea == targetCameraArea)
        {
            SetChildrenActive(true);
        }
    }

    /// <summary>
    /// プレイヤーがいずれかのエリアから出た時の処理
    /// </summary>
    private void HandlePlayerExitedArea(CameraMoveArea exitedArea)
    {
        // プレイヤーが出たエリアが、自分が監視しているエリアなら子を無効化
        if (exitedArea == targetCameraArea)
        {
            SetChildrenActive(false);
        }
    }

    #endregion

    #region トラッカー管理

    /// <summary>
    /// 全ての子オブジェクトにParentTrackerを追加し、管理リストに登録します。
    /// Start時に一度だけ呼ばれます。
    /// </summary>
    private void InitializeTrackers()
    {
        managedTrackers.Clear();

        // transform経由で現在の子オブジェクトを走査
        foreach (Transform child in transform)
        {
            RegisterTracker(child);
        }
    }

    /// <summary>
    /// 対象のオブジェクトにTrackerコンポーネントを追加し、管理リストに入れます。
    /// </summary>
    private void RegisterTracker(Transform target)
    {
        if (target == null)
            return;

        // 既に持っている場合は取得、なければ追加
        EnemyParentTracker tracker = target.GetComponent<EnemyParentTracker>();
        if (tracker == null)
        {
            tracker = target.gameObject.AddComponent<EnemyParentTracker>();
        }

        // 本来の親（自分）を記憶させる
        tracker.Initialize(transform);
        managedTrackers.Add(tracker);
    }

    #endregion

    #region コアロジック

    /// <summary>
    /// 子オブジェクトのアクティブ状態を設定します。
    /// </summary>
    /// <param name="isActive">有効にする場合はtrue、無効にする場合はfalse</param>
    private void SetChildrenActive(bool isActive)
    {
        // 子オブジェクトと追加の管理対象が重複している場合の二重処理を防ぐ
        var processedObjects = new HashSet<GameObject>();

        // Step 1: 親子関係の修復
        // 処理を開始する前に、すべての管理対象オブジェクトを親元に強制送還させる
        // これにより、リフトなどで親子関係が変わっていても、このActivatorの下に戻ってくる
        foreach (var tracker in managedTrackers)
        {
            // オブジェクトが破棄されている場合はスキップ
            if (tracker == null)
                continue;

            tracker.ReturnToOriginalParent();
        }

        // Step 2: プレイヤーがエリアに入った時の有効化処理
        if (isActive)
        {
            // レア敵の判定用にセットを作成（高速検索用）
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

            // 条件付き管理対象と、現在のフラグ条件を満たす対象を収集
            var conditionalEnemySet = new HashSet<GameObject>();
            var activeConditionalEnemySet = new HashSet<GameObject>();
            CollectConditionalEnemies(conditionalEnemySet, activeConditionalEnemySet);

            // Trackerによって親元に戻っているので、transformループで正しく取得可能
            // まずは通常の敵（レア敵・条件付き管理対象のリストにないもの）を有効化
            foreach (Transform child in transform)
            {
                if (child == null)
                    continue;

                if (
                    !rareEnemySet.Contains(child.gameObject)
                    && !conditionalEnemySet.Contains(child.gameObject)
                )
                {
                    child.gameObject.SetActive(true);
                    ResetChildState(child);
                    processedObjects.Add(child.gameObject);
                }
            }

            // 次にレア敵の出現判定と有効化
            if (rareEnemies != null)
            {
                foreach (var rareInfo in rareEnemies)
                {
                    if (rareInfo.enemyObject == null)
                        continue;

                    // 条件付き管理対象としても登録されている場合は、フラグ条件を優先する
                    if (conditionalEnemySet.Contains(rareInfo.enemyObject))
                        continue;

                    // 0から100までの乱数を生成し、出現確率と比較
                    bool shouldSpawn = ShouldSpawnByChance(rareInfo.spawnChance);
                    rareInfo.enemyObject.SetActive(shouldSpawn);
                    processedObjects.Add(rareInfo.enemyObject);

                    if (shouldSpawn)
                    {
                        ResetChildState(rareInfo.enemyObject.transform);
                    }
                }
            }

            // 子ではない追加の管理対象は、通常の敵と同様に必ず有効化する
            if (additionalManagedObjects != null)
            {
                foreach (GameObject managedObject in additionalManagedObjects)
                {
                    if (
                        managedObject == null
                        || conditionalEnemySet.Contains(managedObject)
                        || !processedObjects.Add(managedObject)
                    )
                        continue;

                    managedObject.SetActive(true);
                    ResetChildState(managedObject.transform);
                }
            }

            // 条件付き管理対象は、いずれかのグループの条件を満たした場合だけ有効化する
            foreach (GameObject conditionalEnemy in conditionalEnemySet)
            {
                if (conditionalEnemy == null || !processedObjects.Add(conditionalEnemy))
                    continue;

                bool shouldSpawn = activeConditionalEnemySet.Contains(conditionalEnemy);
                conditionalEnemy.SetActive(shouldSpawn);

                if (shouldSpawn)
                {
                    ResetChildState(conditionalEnemy.transform);
                }
            }
        }
        // Step 3: プレイヤーがエリアから出た時の無効化処理
        else
        {
            // 返却すべきアイテム（PoolableObjectとして扱う）を一時リストに格納する
            // 型を GameObject から DropItem (または PoolableObject) に変更するとスムーズ
            List<DropItem> itemsToReturn = new List<DropItem>();

            foreach (Transform child in transform)
            {
                if (child == null)
                    continue;

                DeactivateManagedObject(child.gameObject, processedObjects, itemsToReturn);
            }

            if (additionalManagedObjects != null)
            {
                foreach (GameObject managedObject in additionalManagedObjects)
                {
                    DeactivateManagedObject(managedObject, processedObjects, itemsToReturn);
                }
            }

            if (conditionalEnemyGroups != null)
            {
                foreach (ConditionalEnemyGroup conditionalGroup in conditionalEnemyGroups)
                {
                    if (conditionalGroup == null)
                        continue;

                    if (conditionalGroup.enemyObjects != null)
                    {
                        foreach (GameObject enemyObject in conditionalGroup.enemyObjects)
                        {
                            DeactivateManagedObject(enemyObject, processedObjects, itemsToReturn);
                        }
                    }

                    if (conditionalGroup.rareEnemies != null)
                    {
                        foreach (RareEnemyInfo rareEnemy in conditionalGroup.rareEnemies)
                        {
                            if (rareEnemy == null)
                                continue;

                            DeactivateManagedObject(
                                rareEnemy.enemyObject,
                                processedObjects,
                                itemsToReturn
                            );
                        }
                    }
                }
            }

            // 各アイテムのメソッドを呼ぶだけ
            foreach (var item in itemsToReturn)
            {
                if (item != null)
                {
                    item.ReturnToPool();
                }
            }
        }
    }

    /// <summary>
    /// 条件付き管理対象の全候補と、現在のフラグ条件を満たす候補を収集します。
    /// </summary>
    private void CollectConditionalEnemies(
        HashSet<GameObject> conditionalEnemySet,
        HashSet<GameObject> activeConditionalEnemySet
    )
    {
        if (conditionalEnemyGroups == null)
            return;

        foreach (ConditionalEnemyGroup conditionalGroup in conditionalEnemyGroups)
        {
            if (conditionalGroup == null)
                continue;

            bool areConditionsMet = conditionalGroup.AreAllFlagsMet();

            if (conditionalGroup.enemyObjects != null)
            {
                foreach (GameObject enemyObject in conditionalGroup.enemyObjects)
                {
                    if (enemyObject == null)
                        continue;

                    conditionalEnemySet.Add(enemyObject);

                    if (areConditionsMet)
                    {
                        activeConditionalEnemySet.Add(enemyObject);
                    }
                }
            }

            if (conditionalGroup.rareEnemies != null)
            {
                foreach (RareEnemyInfo rareEnemy in conditionalGroup.rareEnemies)
                {
                    if (rareEnemy == null || rareEnemy.enemyObject == null)
                        continue;

                    conditionalEnemySet.Add(rareEnemy.enemyObject);

                    if (
                        areConditionsMet
                        && ShouldSpawnByChance(rareEnemy.spawnChance)
                    )
                    {
                        activeConditionalEnemySet.Add(rareEnemy.enemyObject);
                    }
                }
            }
        }
    }

    /// <summary>
    /// パーセント指定の出現確率を判定します。0%と100%は乱数を使わず確定させます。
    /// </summary>
    private bool ShouldSpawnByChance(float spawnChance)
    {
        if (spawnChance <= 0f)
            return false;

        if (spawnChance >= 100f)
            return true;

        return Random.Range(0f, 100f) < spawnChance;
    }

    /// <summary>
    /// 管理対象を無効化します。DropItemの場合は非アクティブ化の代わりにプールへ返却します。
    /// </summary>
    private void DeactivateManagedObject(
        GameObject managedObject,
        HashSet<GameObject> processedObjects,
        List<DropItem> itemsToReturn
    )
    {
        if (managedObject == null || !processedObjects.Add(managedObject))
            return;

        DropItem dropItem = managedObject.GetComponent<DropItem>();
        if (dropItem != null)
        {
            itemsToReturn.Add(dropItem);
        }
        else
        {
            managedObject.SetActive(false);
        }
    }

    /// <summary>
    /// 子オブジェクトが持つIEnemyResettableインターフェースを呼び出し、状態（HPや位置など）をリセットします。
    /// </summary>
    private void ResetChildState(Transform child)
    {
        // 子オブジェクトにアタッチされているすべてのIEnemyResettableコンポーネントを取得
        IEnemyResettable[] resettables = child.GetComponents<IEnemyResettable>();

        if (resettables.Length > 0)
        {
            foreach (IEnemyResettable resettable in resettables)
            {
                resettable.ResetState();
            }
        }
    }

    #endregion

    #region デバッグ / エディタ

#if UNITY_EDITOR
    /// <summary>
    /// 対象のCameraMoveAreaのBoxCollider2Dに自身のコライダーを同期させます。
    /// </summary>
    private void SyncCollider()
    {
        if (!syncColliderWithArea || targetCameraArea == null)
            return;

        if (activationZone == null)
            activationZone = GetComponent<BoxCollider2D>();

        if (activationZone == null)
            return;

        BoxCollider2D targetBox = targetCameraArea.GetComponent<BoxCollider2D>();
        if (targetBox == null)
            return;

        // ターゲットのワールド座標での中心を計算
        Vector3 targetWorldCenter = targetCameraArea.transform.TransformPoint(
            (Vector3)targetBox.offset
        );

        // スケールを考慮したサイズの計算
        Vector2 targetWorldSize = new Vector2(
            targetBox.size.x * Mathf.Abs(targetCameraArea.transform.lossyScale.x),
            targetBox.size.y * Mathf.Abs(targetCameraArea.transform.lossyScale.y)
        );

        //　オフセットの適用
        if (applyCustomOffset)
        {
            // 現在の上下左右の端のワールド座標を計算
            float left = targetWorldCenter.x - targetWorldSize.x / 2f;
            float right = targetWorldCenter.x + targetWorldSize.x / 2f;
            float bottom = targetWorldCenter.y - targetWorldSize.y / 2f;
            float top = targetWorldCenter.y + targetWorldSize.y / 2f;

            // 各端にオフセットを加算
            left += offsetLeft;
            right -= offsetRight;
            bottom += offsetBottom;
            top -= offsetTop;

            // 新しい中心とサイズを計算
            targetWorldCenter.x = (left + right) / 2f;
            targetWorldCenter.y = (bottom + top) / 2f;

            // サイズが 0 以下にならないように制限
            targetWorldSize.x = Mathf.Max(0.0001f, right - left);
            targetWorldSize.y = Mathf.Max(0.0001f, top - bottom);
        }

        // 自身のローカル座標系でのオフセットに変換（自身のTransformは動かさない）
        Vector3 localOffset = transform.InverseTransformPoint(targetWorldCenter);
        Vector2 newOffset = new Vector2(localOffset.x, localOffset.y);

        // 自身のスケールで割り戻してローカルサイズを決定（ゼロ除算防止）
        float scaleX = Mathf.Abs(transform.lossyScale.x);
        float scaleY = Mathf.Abs(transform.lossyScale.y);
        Vector2 newSize = new Vector2(
            targetWorldSize.x / (scaleX != 0f ? scaleX : 1f),
            targetWorldSize.y / (scaleY != 0f ? scaleY : 1f)
        );

        // 値が変わった場合のみ代入
        if (activationZone.offset != newOffset || activationZone.size != newSize)
        {
            // エディタのUndoシステムに登録し、シーンを保存対象（Dirty）にする
            UnityEditor.Undo.RecordObject(activationZone, "Sync Collider with CameraMoveArea");
            activationZone.offset = newOffset;
            activationZone.size = newSize;
            UnityEditor.EditorUtility.SetDirty(activationZone);
        }
    }
#endif

    private void OnDrawGizmos()
    {
#if UNITY_EDITOR
        // 設定画面でカスタムギズモ表示がオフになっている場合は描画しない（デフォルトはtrue）
        if (!UnityEditor.EditorPrefs.GetBool("MyGame_ShowCustomGizmos", true))
        {
            return;
        }
#endif

        // Awake前（編集中）でも動作するように、nullなら取得を試みる
        if (activationZone == null)
        {
            activationZone = GetComponent<BoxCollider2D>();
        }

        if (activationZone != null)
        {
            Color fillColor = new Color(0f, 1f, 0f, 0.1f); // 半透明の緑
            Color borderColor = Color.green;

            // BoxCollider2Dの範囲情報を使ってGizmoを描画
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = fillColor;
            Gizmos.DrawCube(activationZone.offset, activationZone.size);
            Gizmos.color = borderColor;
            Gizmos.DrawWireCube(activationZone.offset, activationZone.size);
        }
    }

    #endregion
}
