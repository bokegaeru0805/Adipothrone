using System.Collections.Generic;
using UnityEngine;
using NaughtyAttributes;

/// <summary>
/// 配置する子オブジェクトの同期設定をまとめたクラス
/// </summary>
[System.Serializable]
public class ChildSpriteSyncSetting
{
    [Tooltip("サイズを同期させる子オブジェクトのSpriteRendererを指定してください")]
    public SpriteRenderer childSpriteRenderer;

    [Tooltip("自動追従時に上下左右の端からのオフセットを加算するかどうか")]
    public bool applyCustomOffset = false;

    [Tooltip("上端のオフセット（正の値で下へ、負の値で上へ移動）")]
    [AllowNesting, ShowIf(nameof(applyCustomOffset))]
    public float offsetTop = 0f;

    [Tooltip("下端のオフセット（正の値で上へ、負の値で下へ移動）")]
    [AllowNesting, ShowIf(nameof(applyCustomOffset))]
    public float offsetBottom = 0f;

    [Tooltip("左端のオフセット（正の値で右へ、負の値で左へ移動）")]
    [AllowNesting, ShowIf(nameof(applyCustomOffset))]
    public float offsetLeft = 0f;

    [Tooltip("右端のオフセット（正の値で左へ、負の値で右へ移動）")]
    [AllowNesting, ShowIf(nameof(applyCustomOffset))]
    public float offsetRight = 0f;
}

/// <summary>
/// 親のSpriteRendererのTiledモードに合わせて、複数の子オブジェクトのサイズと位置を自動同期するスクリプト
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(SpriteRenderer))]
public class TiledSpriteSynchronizer : MonoBehaviour
{
    #region インスペクター設定

    [Header("連携設定")]
    [SerializeField, Tooltip("サイズを同期させる子オブジェクトの設定リスト")]
    private List<ChildSpriteSyncSetting> syncSettings = new List<ChildSpriteSyncSetting>();

    #endregion

    #region プライベート変数

    private SpriteRenderer parentSpriteRenderer;
    private Vector2 previousSize;

    #endregion

    #region Unityイベント関数

    private void Start()
    {
        parentSpriteRenderer = GetComponent<SpriteRenderer>();
        if (parentSpriteRenderer != null)
        {
            previousSize = parentSpriteRenderer.size;
        }
    }

    private void Update()
    {
#if UNITY_EDITOR
        // ゲーム実行中は処理を行わない（パフォーマンス低下を防ぐため）
        if (Application.isPlaying)
            return;

        if (parentSpriteRenderer == null)
            parentSpriteRenderer = GetComponent<SpriteRenderer>();

        if (parentSpriteRenderer == null)
            return;

        // 親のSpriteRendererの描画モードがTiledでない場合は処理しない
        if (parentSpriteRenderer.drawMode != SpriteDrawMode.Tiled)
            return;

        // 親のサイズに変更があった場合のみ、子オブジェクトのサイズを更新する
        if (parentSpriteRenderer.size != previousSize)
        {
            UpdateChildrenSize();
            previousSize = parentSpriteRenderer.size;
        }
#endif
    }

    #endregion

    #region 同期処理

    /// <summary>
    /// 現在の親のSpriteRendererのサイズに合わせて、登録された全ての子オブジェクトのサイズと位置を更新する
    /// </summary>
    [Button("更新")]
    private void UpdateChildrenSize()
    {
        if (syncSettings == null || syncSettings.Count == 0) 
            return;

        Vector2 parentSize = parentSpriteRenderer.size;

        foreach (var setting in syncSettings)
        {
            if (setting.childSpriteRenderer == null) 
                continue;

            Vector2 newSize = parentSize;
            Vector3 newLocalPosition = setting.childSpriteRenderer.transform.localPosition;

            if (setting.applyCustomOffset)
            {
                // 親の各端からのオフセットを適用し、新しいサイズとローカル座標を計算する
                float left = (-parentSize.x / 2f) + setting.offsetLeft;
                float right = (parentSize.x / 2f) - setting.offsetRight;
                float bottom = (-parentSize.y / 2f) + setting.offsetBottom;
                float top = (parentSize.y / 2f) - setting.offsetTop;

                // サイズの計算（0以下にならないように制限）
                newSize.x = Mathf.Max(0.0001f, right - left);
                newSize.y = Mathf.Max(0.0001f, top - bottom);

                // 新しい中心位置（ローカル座標）の計算
                newLocalPosition.x = (left + right) / 2f;
                newLocalPosition.y = (bottom + top) / 2f;
            }
            else
            {
                // オフセットがない場合は親の中心に配置
                newLocalPosition.x = 0f;
                newLocalPosition.y = 0f;
            }

            ApplyChangesIfNecessary(setting.childSpriteRenderer, newSize, newLocalPosition);
        }

        Debug.Log("子オブジェクトのTiledサイズと位置を親と同期しました");
    }

    /// <summary>
    /// サイズや位置に変更がある場合のみ、対象のSpriteRendererに値を適用する
    /// </summary>
    /// <param name="targetRenderer">更新対象の子SpriteRenderer</param>
    /// <param name="newSize">新しいサイズ</param>
    /// <param name="newPosition">新しいローカル座標</param>
    private void ApplyChangesIfNecessary(SpriteRenderer targetRenderer, Vector2 newSize, Vector3 newPosition)
    {
        // 値が変わった場合のみ更新処理を行う
        if (targetRenderer.size != newSize || targetRenderer.transform.localPosition != newPosition)
        {
#if UNITY_EDITOR
            // エディタのUndoシステムに登録し、シーンを保存対象（Dirty）にする
            UnityEditor.Undo.RecordObject(targetRenderer, "Sync Child Sprite Size");
            UnityEditor.Undo.RecordObject(targetRenderer.transform, "Sync Child Sprite Position");
#endif

            targetRenderer.size = newSize;
            targetRenderer.transform.localPosition = newPosition;

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(targetRenderer);
            UnityEditor.EditorUtility.SetDirty(targetRenderer.transform);
#endif
        }
    }

    #endregion
}