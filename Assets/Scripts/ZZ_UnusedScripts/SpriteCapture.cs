// using System.IO; // ファイルの読み書きに必要
// using UnityEngine;

// /// <summary>
// /// 指定されたSpriteRendererの見た目をキャプチャし、PNGファイルとして保存するクラス。
// /// </summary>
// public class SpriteCapture : MonoBehaviour
// {
//     [Header("設定")]
//     [Tooltip("キャプチャしたい対象のSpriteRenderer")]
//     [SerializeField]
//     private SpriteRenderer targetSpriteRenderer;

//     [Tooltip("キャプチャに使用する専用のカメラ")]
//     [SerializeField]
//     private Camera renderCamera;

//     /// <summary>
//     /// キャプチャを実行し、PNGとして保存します。UIのボタンなどから呼び出します。
//     /// </summary>
//     public void CaptureAndSaveSprite()
//     {
//         if (targetSpriteRenderer == null || renderCamera == null)
//         {
//             Debug.LogError("対象のSpriteRendererまたはRenderCameraが設定されていません。");
//             return;
//         }

//         // --- ステップ1: RenderTextureの準備 ---
//         // スプライトの元のテクスチャサイズを取得
//         int width = targetSpriteRenderer.sprite.texture.width;
//         int height = targetSpriteRenderer.sprite.texture.height;

//         // 撮影用のデジタルフィルム（RenderTexture）を作成
//         RenderTexture renderTexture = new RenderTexture(
//             width,
//             height,
//             24,
//             RenderTextureFormat.ARGB32
//         );

//         // RenderTextureにアンチエイリアス（ぼかし）がかからないようにPointフィルタを設定
//         renderTexture.filterMode = FilterMode.Point;

//         // --- ステップ2: カメラの設定と配置 ---
//         // カメラの描画先を、今作成したRenderTextureに設定
//         renderCamera.targetTexture = renderTexture;
//         // 背景が透明になるように設定
//         renderCamera.clearFlags = CameraClearFlags.SolidColor;
//         renderCamera.backgroundColor = new Color(0, 0, 0, 0); // 透明な黒

//         // スプライトを正確に撮影するためのカメラ位置とサイズを調整
//         Bounds spriteBounds = targetSpriteRenderer.bounds;
//         renderCamera.orthographic = true;
//         renderCamera.orthographicSize = spriteBounds.extents.y;
//         renderCamera.transform.position = new Vector3(
//             spriteBounds.center.x,
//             spriteBounds.center.y,
//             -10
//         );

//         // --- ステップ3: レンダリング（撮影） ---
//         // カメラに1フレームだけレンダリングを実行させる
//         renderCamera.Render();

//         // --- ステップ4: ピクセルデータの読み込み ---
//         // GPU上のRenderTextureからピクセルを読み込むためのCPU側テクスチャ（Texture2D）を作成
//         Texture2D pngTexture = new Texture2D(width, height, TextureFormat.ARGB32, false);

//         // アクティブなRenderTextureを一時的に切り替えてピクセルを読み込む
//         RenderTexture.active = renderTexture;
//         pngTexture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
//         pngTexture.Apply();

//         // --- ステップ5: PNGへのエンコードと保存 ---
//         byte[] pngData = pngTexture.EncodeToPNG();
//         if (pngData != null)
//         {
//             // ファイルパスを決定（プラットフォーム共通で書き込み可能な場所）
//             string path = Path.Combine(Application.persistentDataPath, "CapturedSprite.png");
//             File.WriteAllBytes(path, pngData);
//             Debug.Log($"スプライトをPNGとして保存しました: {path}");
//         }

//         // --- ステップ6: 後片付け ---
//         // アクティブなRenderTextureを元に戻す（非常に重要）
//         RenderTexture.active = null;
//         // カメラの描画先を元に戻す
//         renderCamera.targetTexture = null;
//         // 作成したリソースを解放
//         Destroy(pngTexture);
//         renderTexture.Release();
//     }
// }