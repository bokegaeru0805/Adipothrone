using UnityEngine;

public class OutlineController : MonoBehaviour
{
    private GameObject parentObject; // 親オブジェクトを参照するための変数
    private Material material; // マテリアル設定を参照するための変数
    private bool previousWasDamageable; // 前回のタグが "Damageable" だったか
    private bool previousWasImmune; // 前回のタグが "Immune" だったか
    private SpriteRenderer myRenderer; // 自身（アウトライン）のSpriteRenderer
    private SpriteRenderer parentRenderer; // 親（本体）のSpriteRenderer
    private Sprite previousSprite; // 前回のスプライトを保存するための変数

    private void Awake()
    {
        // 自身のコンポーネントをキャッシュ
        myRenderer = GetComponent<SpriteRenderer>();
        if (myRenderer == null)
        {
            Debug.LogError($"{this.gameObject}にSpriteRendererがアタッチされていません。");
            return;
        }
        material = myRenderer.material;

        if (material == null)
        {
            Debug.LogError($"{this.gameObject}にMaterialがアタッチされていません。");
        }

        // 親オブジェクトとそのSpriteRendererを取得・キャッシュ
        parentObject = transform.parent.gameObject;
        if (parentObject == null)
        {
            Debug.LogError($"{this.gameObject}の親オブジェクトが見つかりません。");
            return;
        }
        parentRenderer = parentObject.GetComponent<SpriteRenderer>();
        if (parentRenderer == null)
        {
            Debug.LogError($"{parentObject.name}にSpriteRendererが見つかりません。");
            return;
        }
    }

    // private void Start()
    // {
    //     // 初回同期を実行（isOutlineCurrentlyActive の初期化を含む）
    //     SyncWithParent();
    // }

    private void LateUpdate()
    {
        // CompareTag() はGCを発生させないため、タグの状態をチェックする
        bool isCurrentlyDamageable = parentObject.CompareTag(GameConstants.DamageableEnemyTagName);
        bool isCurrentlyImmune = parentObject.CompareTag(GameConstants.ImmuneEnemyTagName);

        // タグの状態が前回から変更されたか (bool同士の比較)
        bool tagStateChanged =
            (isCurrentlyDamageable != previousWasDamageable)
            || (isCurrentlyImmune != previousWasImmune);

        // 親の状態（スプライト、タグ状態、左右反転）のいずれかが変更されているかチェック
        if (
            parentRenderer.sprite != previousSprite
            || tagStateChanged
            || myRenderer.flipX != parentRenderer.flipX
        )
        {
            //変更があれば同期処理を実行
            // 現在の状態を引数として渡す
            SyncWithParent(isCurrentlyDamageable, isCurrentlyImmune);
        }
    }

    /// <summary>
    /// 親オブジェクトの状態と自身を同期させる
    /// </summary>
    private void SyncWithParent(bool isDamageable, bool isImmune)
    {
        // 1. スプライトを同期する
        previousSprite = parentRenderer.sprite;
        myRenderer.sprite = previousSprite;

        // 2. 左右反転(flipX)を同期する
        myRenderer.flipX = parentRenderer.flipX;

        // 3. タグに応じたマテリアル（アウトライン色）を設定する
        // 現在の状態を引数として渡す
        SetMaterialBasedOnTag(isDamageable, isImmune);

        // 4. 現在のタグ状態を記録する
        previousWasDamageable = isDamageable;
        previousWasImmune = isImmune;
    }

    /// <summary>
    /// タグに応じてマテリアルを設定するメソッド
    /// </summary>
    private void SetMaterialBasedOnTag(bool isDamageable, bool isImmune)
    {
        if (material == null)
        {
            return;
        }

        // タグに応じてマテリアルを設定する
        if (isDamageable)
        {
            material.SetFloat("_OutlineAlpha", 1f); // アウトラインを有効化
            material.SetColor("_OutlineColor", new Color(128f / 255f, 0 / 255f, 0f / 255f, 1f));
        }
        else if (isImmune)
        {
            material.SetFloat("_OutlineAlpha", 1f); // アウトラインを有効化
            material.SetColor("_OutlineColor", new Color(13f / 128f, 128f / 255f, 0f / 255f, 1f));
        }
        else
        {
            material.SetFloat("_OutlineAlpha", 0f); // アウトラインを無効化
        }
    }
}
