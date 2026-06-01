using System.Collections.Generic;
using NaughtyAttributes;
using UnityEngine;

/// <summary>
/// 配置するオブジェクトの設定をまとめたクラス
/// </summary>
[System.Serializable]
public class SpawnSetting
{
    [Tooltip("インスペクターのList要素名に反映されるオブジェクト名")]
    public string objectName = "Pillar";

    [Tooltip("配置したいプレハブ")]
    public GameObject prefabToSpawn;

    [Tooltip("左下からのX軸のオフセット")]
    public float offsetX = 0f;

    [Tooltip("左下からのY軸のオフセット")]
    public float offsetY = 0f;

    [Tooltip("配置するX間隔")]
    public float intervalX = 2.56f;

    [Tooltip("下から作成をスキップする段数")]
    public int skipBottomRows = 0;

    [Tooltip("上から作成をスキップする段数")]
    public int skipTopRows = 0;
}

/// <summary>
/// SpriteRendererのTiledモードに合わせて、指定したオブジェクトを自動配置するスクリプト
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(SpriteRenderer))]
public class TiledObjectSpawner : MonoBehaviour
{
    #region フィールド / 変数

    [SerializeField, Tooltip("配置するオブジェクトの設定リスト")]
    private List<SpawnSetting> spawnSettings = new List<SpawnSetting>();

    [SerializeField, Tooltip("再配置時に削除（クリア）から除外するオブジェクトのリスト")]
    private List<GameObject> excludeFromDeletion = new List<GameObject>();

    private SpriteRenderer spriteRenderer;
    private Vector2 lastSize;

    #endregion

    #region Unityイベント関数

    private void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Update()
    {
        // ゲーム実行中は処理を行わない（パフォーマンス低下を防ぐため）
        if (Application.isPlaying)
            return;

        if (spriteRenderer == null)
            return;
        if (spriteRenderer.drawMode != SpriteDrawMode.Tiled)
            return;

        // SpriteRendererのサイズが変更された時だけ処理を実行する
        if (spriteRenderer.size != lastSize)
        {
            UpdateSpawns();
            lastSize = spriteRenderer.size;
        }
    }

    #endregion

    #region メイン処理

    /// <summary>
    /// 現在のSpriteRendererのサイズに合わせてオブジェクトを再配置する
    /// </summary>
    [Button("更新")]
    private void UpdateSpawns()
    {
        // 既存の生成物（子オブジェクト）を削除する
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            GameObject childObj = transform.GetChild(i).gameObject;

            // 除外リストに登録されているオブジェクトなら削除をスキップする
            if (excludeFromDeletion.Contains(childObj))
            {
                continue;
            }

            if (Application.isPlaying)
            {
                Destroy(childObj);
            }
            else
            {
                DestroyImmediate(childObj);
            }
        }

        // スプライトが設定されていない場合は処理をスキップ
        if (spriteRenderer.sprite == null)
            return;

        // 配置y間隔は元のpngの縦幅を読み取り、それに応じて配置する（縦幅と横幅の自動取得）
        float tileWidth = spriteRenderer.sprite.bounds.size.x;
        float tileHeight = spriteRenderer.sprite.bounds.size.y;

        // 配置の開始位置（左端・下端）を計算する
        // 左下を基準に絶対座標（ユニット単位）で配置するためのベース座標
        Vector3 localBottomLeft = new Vector3(
            -spriteRenderer.size.x / 2f,
            -spriteRenderer.size.y / 2f,
            0f
        );

        // Tiledで繰り返される回数を計算する（縦方向のタイル数）
        int tileCountY = Mathf.CeilToInt(spriteRenderer.size.y / tileHeight);
        if (tileCountY < 1)
            tileCountY = 1;

        // リストに登録されたすべての設定についてループ処理を行う
        foreach (var setting in spawnSettings)
        {
            if (setting.prefabToSpawn == null || setting.intervalX <= 0f)
                continue;

            int tileCountX = Mathf.CeilToInt(spriteRenderer.size.x / setting.intervalX);

            for (int j = 0; j < tileCountY; j++)
            {
                // 上下指定した段分は作成処理をスキップする（設定ごとに個別に判定）
                if (j < setting.skipBottomRows || j >= (tileCountY - setting.skipTopRows))
                    continue;

                // Y軸がSpriteRendererのサイズ（背景の広がり）を超えている場合は配置しない
                if (setting.offsetY + (tileHeight * j) > spriteRenderer.size.y)
                    continue;

                for (int i = 0; i < tileCountX; i++)
                {
                    // 配置幅の制限を超えないように制御（X軸が背景の広がりを超えている場合は配置しない）
                    if (setting.offsetX + (setting.intervalX * i) > spriteRenderer.size.x)
                        continue;

                    // 柱のX座標 ＝ 左端の基準位置 ＋ 微調整のオフセット ＋ (配置x間隔 × 何番目か)
                    float posX = localBottomLeft.x + setting.offsetX + (setting.intervalX * i);
                    // Y座標は元のpngの縦幅（tileHeight）を基準に、縦の繰り返し回数に応じて配置
                    float posY = localBottomLeft.y + setting.offsetY + (tileHeight * j);

                    GameObject spawned = Instantiate(setting.prefabToSpawn, transform);
                    spawned.transform.localPosition = new Vector3(posX, posY, 0f);

                    // 指定した名前に変更する
                    spawned.name = setting.objectName + "_" + j + "_" + i;
                }
            }
        }

        Debug.Log("オブジェクトを再配置しました。画像縦幅: " + tileHeight);
    }

    #endregion
}
