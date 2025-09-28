using UnityEngine;

public class OutlineController : MonoBehaviour
{
    private GameObject parentObject; // 親オブジェクトを参照するための変数
    private Material material; // マテリアル設定を参照するための変数
    private string previousTag; // 前回のタグを保存するための変数

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

    private void Start()
    {
        previousTag = parentObject.tag; // 初期タグを記録
        // 初回同期を実行
        SyncWithParent();
    }

    private void LateUpdate()
    {
        // 親の状態（スプライト、タグ、左右反転）のいずれかが変更されているかチェック
        if (
            parentRenderer.sprite != previousSprite
            || parentObject.tag != previousTag
            || myRenderer.flipX != parentRenderer.flipX
        )
        {
            // 変更があれば同期処理を実行
            SyncWithParent();
        }
    }

    /// <summary>
    /// 親オブジェクトの状態と自身を同期させる
    /// </summary>
    private void SyncWithParent()
    {
        // 1. スプライトを同期する
        previousSprite = parentRenderer.sprite;
        myRenderer.sprite = previousSprite;

        // 2. 左右反転(flipX)を同期する
        myRenderer.flipX = parentRenderer.flipX;

        // 3. タグに応じたマテリアル（アウトライン色）を設定する
        SetMaterialBasedOnTag();

        // 4. 現在のタグを記録する
        previousTag = parentObject.tag;
    }

    /// <summary>
    /// タグに応じてマテリアルを設定するメソッド
    /// </summary>
    private void SetMaterialBasedOnTag()
    {
        if (material == null)
        {
            return;
        }

        // タグに応じてマテリアルを設定する
        if (parentObject.tag == GameConstants.DamageableEnemyTagName)
        {
            material.SetFloat("_OutlineAlpha", 1f); // アウトラインを有効化
            material.SetColor("_OutlineColor", new Color(128f / 255f, 0 / 255f, 0f / 255f, 1f));
        }
        else if (parentObject.tag == GameConstants.ImmuneEnemyTagName)
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
