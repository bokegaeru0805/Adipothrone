using UnityEngine;

/// <summary>
/// 他のCollider2Dと接触した際に、そのオブジェクト名とプレイヤーとの距離をデバッグ表示するクラス
/// </summary>
public class CollisionDebugger : MonoBehaviour
{
    [Header("参照")]
    [SerializeField]
    [Tooltip("距離を測定する対象となるプレイヤーのTransform")]
    private Transform playerTransform;

    private void Start()
    {
        if (playerTransform == null)
        {
            Debug.Log("プレイヤーのTransformが設定されていません。");
        }
    }

    /// <summary>
    /// isTriggerがfalseのコライダー同士が物理的に衝突した瞬間に呼び出される
    /// </summary>
    private void OnCollisionEnter2D(Collision2D collision)
    {
        // 接触情報をログに出力する共通メソッドを呼び出す
        LogCollisionInfo(collision.gameObject);
    }

    /// <summary>
    /// isTriggerがtrueのコライダーが他のコライダーと接触した瞬間に呼び出される
    /// </summary>
    private void OnTriggerEnter2D(Collider2D other)
    {
        // 接触情報をログに出力する共通メソッドを呼び出す
        LogCollisionInfo(other.gameObject);
    }

    /// <summary>
    /// 接触したオブジェクトの情報を色付きでコンソールに出力する
    /// </summary>
    /// <param name="otherObject">接触した相手のGameObject</param>
    private void LogCollisionInfo(GameObject otherObject)
    {
        // Inspectorでプレイヤーが設定されていない場合は、警告を出して処理を中断
        if (playerTransform == null)
        {
            Debug.LogWarning("Player Transformが設定されていません。距離を測定できません。", this);
            return;
        }

        // 1. 接触したオブジェクトの名前を取得
        string objectName = otherObject.name;

        // 2. プレイヤーの現在位置と、自分のオブジェクトの位置との距離を計算
        float distance = Vector2.Distance(playerTransform.position, this.gameObject.transform.position);

        // 3. 色付きで表示するためのログメッセージを作成
        //    <color=cyan>...</color> : リッチテキストタグを使い、文字をシアン（水色）にする
        //    distance:F2 : 距離を小数点以下2桁までで表示する書式設定
        string logMessage = $"<color=cyan>接触オブジェクト: {objectName}, プレイヤーとの距離: {distance:F5}m</color>";
        
        // 4. コンソールにログを出力
        Debug.Log(logMessage);
    }
}