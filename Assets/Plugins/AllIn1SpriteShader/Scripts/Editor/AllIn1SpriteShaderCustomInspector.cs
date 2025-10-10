#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// AllIn1SpriteShaderWithFlashシェーダー用のカスタムインスペクター。
/// 元のインスペクターを継承し、Damage Flash機能のUIを追加する。
/// </summary>
public class AllIn1SpriteShaderWithFlashInspector : AllIn1SpriteShaderMaterialInspector // ← ShaderGUIではなく、元のクラスを継承
{

    // シェーダーがマテリアルに新規設定されたときに呼び出されるメソッド
    public override void AssignNewShaderToMaterial(Material material, Shader oldShader, Shader newShader)
    {
        // まず、親クラスが持つデフォルトの初期設定処理を実行する
        base.AssignNewShaderToMaterial(material, oldShader, newShader);

        // その後、このシェーダー独自のデフォルト値を設定する
        // FLASH_ONキーワードをデフォルトで有効にする
        material.EnableKeyword("FLASH_ON");
    }
    
    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        // --- 1. まず、元のインスペクターが持つ機能をすべて描画する ---
        base.OnGUI(materialEditor, properties);


        // --- 2. ここから、新しい機能のUIを追加で描画する ---

        // シェーダーから追加したプロパティを探す
        MaterialProperty flashAmount = FindProperty("_FlashAmount", properties, false);
        MaterialProperty flashColor = FindProperty("_FlashColor", properties, false);

        // プロパティが見つからなければ何もしない（エラー防止）
        if (flashAmount == null || flashColor == null) return;
        
        // 区切り線とスペースを入れて見やすくする
        EditorGUILayout.Space();
        DrawLine(Color.grey, 1, 3);

        // 追加セクションのタイトルを描画
        EditorGUILayout.LabelField("Damage Flash Effect", EditorStyles.boldLabel);

        // マテリアル本体を取得
        Material targetMat = materialEditor.target as Material;

        // FLASH_ONキーワードの状態に応じてトグルを描画
        bool isFlashOn = targetMat.IsKeywordEnabled("FLASH_ON");
        bool newIsFlashOn = EditorGUILayout.Toggle("Enable Damage Flash", isFlashOn);

        // トグルの状態が変更されたらキーワードを更新
        if (newIsFlashOn != isFlashOn)
        {
            if (newIsFlashOn) targetMat.EnableKeyword("FLASH_ON");
            else targetMat.DisableKeyword("FLASH_ON");
        }

        // トグルがONのときだけ、詳細設定（スライダーとカラーピッカー）を表示
        if (newIsFlashOn)
        {
            EditorGUI.indentLevel++; // インデントを一段下げる
            materialEditor.ShaderProperty(flashAmount, "Flash Amount");
            materialEditor.ShaderProperty(flashColor, "Flash Color");
            EditorGUI.indentLevel--; // インデントを元に戻す
        }
    }
    
    // 独自のDrawLineを定義して、親クラスのprivateメソッドにアクセスできない問題を回避
    private void DrawLine(Color color, int thickness = 2, int padding = 10)
    {
        Rect r = EditorGUILayout.GetControlRect(GUILayout.Height(padding + thickness));
        r.height = thickness;
        r.y += padding / 2;
        r.x -= 2;
        r.width += 6;
        EditorGUI.DrawRect(r, color);
    }
}
#endif