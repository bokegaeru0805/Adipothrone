using System.Collections;
using System.Collections.Generic;
using UnityEngine;
// [追加] EditorApplicationクラスを使用するために必要
#if UNITY_EDITOR
using UnityEditor;
#endif

public class PlayerTestMoveController : MonoBehaviour
{
    // カメラをキャッシュするための変数
    private Camera mainCamera;
    private float zOffset = 10f;

    // [追加] Playモード停止機能のための設定項目
    [Header("エディタ用デバッグ機能")]
    [Tooltip("このキーを指定回数連打するとPlayモードを停止します")]
    [SerializeField]
    private KeyCode stopKey = KeyCode.S;

    [Tooltip("Playモードを停止するために必要なキーの連打回数")]
    [SerializeField]
    private int requiredPressCount = 10;

    [Tooltip("キー連打と判定される最大の間隔（秒）")]
    [SerializeField]
    private float timeWindow = 0.5f;

    // [追加] 連打回数と時間を記録するための内部変数
    private int currentPressCount = 0;
    private float timeSinceLastPress = 0f;

    void Start()
    {
        // 効率化のため、最初にメインカメラを取得しておく
        mainCamera = Camera.main;
        // 連打カウントを初期化
        currentPressCount = 0;
    }

    void Update()
    {
        // 1. マウスのスクリーン座標を取得する
        Vector3 mouseScreenPosition = Input.mousePosition;

        // 2. マウスのスクリーン座標のz座標に、カメラからの距離を設定する
        mouseScreenPosition.z = zOffset;

        // 3. スクリーン座標をワールド座標に変換する
        Vector3 mouseWorldPosition = mainCamera.ScreenToWorldPoint(mouseScreenPosition);

        // 4. オブジェクトの位置を、変換したワールド座標に設定する
        transform.position = mouseWorldPosition;

        // [追加] エディタ内でのみ実行するキー連打チェック処理
#if UNITY_EDITOR
        HandleEditorStop();
#endif
    }

    // [追加] Playモード停止を処理する専用の関数
#if UNITY_EDITOR
    private void HandleEditorStop()
    {
        // 最後のキープレスからの時間を加算
        timeSinceLastPress += Time.deltaTime;

        // もし最後のキープレスから指定時間を超えていたら、連打カウントをリセット
        if (timeSinceLastPress > timeWindow)
        {
            currentPressCount = 0;
        }

        // 指定したキーが押された瞬間を検知
        if (Input.GetKeyDown(stopKey))
        {
            currentPressCount++; // 連打カウントを1増やす
            timeSinceLastPress = 0f; // 最後のキープレスからの時間をリセット

            // 連打カウントが必要な回数に達したら
            if (currentPressCount >= requiredPressCount)
            {
                // Debug.Log(
                //     $"'{stopKey}'キーが{requiredPressCount}回連打されたため、Playモードを停止します。"
                // );
                // Playモードを停止する
                //EditorApplication.isPlaying = false;

                EditorApplication.isPaused = true;
            }
        }
    }
#endif
}
