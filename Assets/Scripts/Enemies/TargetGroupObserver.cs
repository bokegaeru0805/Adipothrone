using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 指定された複数のGameObjectを監視し、すべてが非アクティブになったら複数のイベントを実行する汎用コンポーネント。
/// </summary>
public class TargetGroupObserver : MonoBehaviour
{
    // UnityのInspectorはインターフェース(IDefeatable)のリストを直接表示できない。
    // そのため、MonoBehaviourのリストで一旦受け取り(Raw)、AwakeでIDefeatableに変換する、という手法を取る。
    [Header("監視対象のオブジェクト")]
    [Tooltip("このリストに含まれるIDefeatableを持つオブジェクトがすべて倒されることを監視します。")]
    [SerializeField]
    private List<GameObject> targetsToObserveRaw = new List<GameObject>();

    private List<IDefeatable> targetsToObserve = new List<IDefeatable>(); // プログラムで実際に使用するリスト

    [Header("達成時のイベント")]
    [Tooltip(
        "すべてのターゲットが非アクティブになったときに一度だけ実行される、名前付きのイベントリスト。"
    )]
    [SerializeField]
    private List<NamedEvent> onAllTargetsDeactivated = new List<NamedEvent>();

    // イベントが既に実行されたかを管理するフラグ
    private bool isCompleted = false;

    private void Awake()
    {
        // Inspectorで設定されたGameObjectのリストを走査
        foreach (var targetGo in targetsToObserveRaw)
        {
            // GameObjectがnullの場合はスキップ
            if (targetGo == null)
            {
                continue;
            }

            // GameObjectからIDefeatableインターフェースを持つコンポーネントを検索
            IDefeatable defeatableTarget = targetGo.GetComponent<IDefeatable>();

            // 見つかった場合
            if (defeatableTarget != null)
            {
                // 監視対象リストに追加
                targetsToObserve.Add(defeatableTarget);
            }
            else
            {
                // IDefeatableを持つコンポーネントが見つからなかった場合に警告を出す
                Debug.LogWarning(
                    $"GameObject '{targetGo.name}' には IDefeatable を実装したコンポーネントが見つからないため、監視対象から除外されました。",
                    targetGo
                );
            }
        }
    }

    /// <summary>
    /// 毎フレーム、ターゲットの状態をチェックします。
    /// </summary>
    private void Update()
    {
        if (isCompleted || targetsToObserve.Count == 0)
        {
            return;
        }

        // isDefeatedフラグをチェックするロジックは、より効率的なLINQのAll()に書き換え
        // targetsToObserveリスト内の全ての要素が IsDefeated == true を満たすかチェック
        if (targetsToObserve.TrueForAll(target => target == null || target.IsDefeated))
        {
            Complete();
        }
    }

    /// <summary>
    /// 条件達成時の処理を一度だけ実行します。
    /// </summary>
    private void Complete()
    {
        // 設定されたすべてのイベントを実行
        foreach (var namedEvent in onAllTargetsDeactivated)
        {
            namedEvent.onTriggered?.Invoke();
        }

        // フラグを立て、今後Updateでのチェックが走らないようにする
        isCompleted = true;
    }
}

/// <summary>
/// Inspector上で名前を付けて管理できるUnityEventのデータ構造。
/// </summary>
[System.Serializable]
public class NamedEvent
{
    [Tooltip("このイベントの目的を分かりやすくするための名前。")]
    public UnityEvent onTriggered;
}
