using System.Collections;
using UnityEngine;

public class TitleManager : MonoBehaviour
{
    private void Start()
    {
        StartCoroutine(PlayTitleBGMWhenReady());

        //画面を明転させる
        //ゲームオーバー時など、タイトルに戻る際にフェードインを行うため
        FadeCanvas.instance.FadeIn(0.5f);
    }

    /// <summary>
    /// BGMManagerのインスタンスが利用可能になる（初期化が完了する）まで待機してから
    /// BGMの再生処理を呼び出すコルーチン。
    /// </summary>
    private IEnumerator PlayTitleBGMWhenReady()
    {
        // BGMManager.instance が null である限り、このフレームで待機し続ける
        // (BGMManagerのAwake()やStart()が完了するのを待つ)
        yield return new WaitUntil(() => BGMManager.instance != null);

        // --- WaitUntilが完了した（BGMManager.instance が利用可能になった） ---

        // 元のStart()の処理を実行（この時点では instance は null でないことが保証されている）
        BGMManager.instance.Play(BGMCategory.Title);

        // 元の else (LogError) 処理は、
        // もしBGMManagerがシーンに存在せず、instanceが永久にnullの場合、
        // WaitUntilが永遠に待機し続けることになるため、ここでは削除します。
        // (もしBGMManagerが存在しない可能性があり、タイムアウト処理が必要な場合は別途対応が必要です)
    }

    /// <summary>
    /// 「New Game」ボタンがクリックされたときに呼び出されるメソッド。
    /// ゲームを新規に開始するための処理を実行する。
    /// </summary>
    public void OnNewGameButtonClicked()
    {
        // SaveLoadManagerのnewLoadを呼び出す
        SaveLoadManager.instance.newLoad();
    }
}
