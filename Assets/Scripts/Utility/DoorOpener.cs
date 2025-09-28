using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// プレイヤーを指定位置にワープさせる共通ドアオープン処理
/// </summary>
public class DoorOpener : MonoBehaviour
{
    public enum DoorType
    {
        None = 0,
        MetalDoor = 5,
        WoodenDoor = 10,
        WoodenGate = 25,
        Well = 20,
    }

    /// <summary>
    /// ドアを開けてプレイヤーを指定位置に移動させる
    /// </summary>
    /// <param name="destination">移動先座標</param>
    /// <param name="caller">StartCoroutineするMonoBehaviour</param>
    public static void OpenDoor(
        Vector2 destination,
        MonoBehaviour caller,
        DoorType doorType = DoorType.None
    )
    {
        if (caller != null)
        {
            caller.StartCoroutine(OpenDoorCoroutine(destination, doorType));
        }
    }

    public static IEnumerator OpenDoorCoroutine(Vector2 destination, DoorType doorType)
    {
        // ドアの種類に応じて処理を分岐
        switch (doorType)
        {
            case DoorType.MetalDoor:
                SEManager.instance?.PlayFieldSE(SE_Field.DoorOpen_Metal);
                break;
            case DoorType.WoodenDoor:
                // ウッドンドアの特別な処理があればここに追加
                break;
            case DoorType.Well:
                // 井戸の特別な処理があればここに追加
                break;
            case DoorType.WoodenGate:
                // 木製ゲートの特別な処理があればここに追加
                break;
            default:
                // 特に何もしない
                break;
        }

        // プレイヤーの操作をロック
        var playerManager = PlayerManager.instance;
        playerManager.LockControl();

        FadeCanvas.instance.FadeOut(0.05f); // 画面を暗転させる
        yield return new WaitForSecondsRealtime(0.1f); // 少し待ってフェードが始まるのを確認

        // プレイヤーの位置を移動させ、同時にカメラの追従完了を待つ
        // PlayerMoveがコルーチンを返すので、yield return で待機する
        yield return playerManager.StartCoroutine(playerManager.PlayerMove(destination));

        // ここに到達した時点で、プレイヤーの移動とカメラの追従が完了している

        FadeCanvas.instance.FadeIn(1f / 60f); // 画面を明転させる
        yield return new WaitForSeconds(0.3f); // 連続でドアが開かないように少し待機

        // プレイヤーの操作を再び許可
        playerManager.UnlockControl();
    }
}
