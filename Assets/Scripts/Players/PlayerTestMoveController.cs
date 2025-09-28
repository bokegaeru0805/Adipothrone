using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerTestMoveController : MonoBehaviour
{
    // カメラをキャッシュするための変数
    private Camera mainCamera;
    private float zOffset = 10f; //カメラからオブジェクトまでの距離

    void Start()
    {
        // 効率化のため、最初にメインカメラを取得しておく
        mainCamera = Camera.main;
    }

    void Update()
    {
        // 1. マウスのスクリーン座標を取得する
        //    Input.mousePosition は (x, y, 0) のVector3を返す
        Vector3 mouseScreenPosition = Input.mousePosition;

        // 2. マウスのスクリーン座標のz座標に、カメラからの距離を設定する
        //    これにより、オブジェクトがカメラの視界に正しく表示される
        mouseScreenPosition.z = zOffset;

        // 3. スクリーン座標をワールド座標に変換する
        Vector3 mouseWorldPosition = mainCamera.ScreenToWorldPoint(mouseScreenPosition);

        // 4. オブジェクトの位置を、変換したワールド座標に設定する
        transform.position = mouseWorldPosition;
    }
}
