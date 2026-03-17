using System.Collections;
using UnityEngine;

public class TitleManager : MonoBehaviour
{
    // 解放用のコマンド順番（W, W, S, S, A, D, A, D）
    private readonly KeyCode[] unlockSequence = new KeyCode[]
    {
        KeyCode.W,
        KeyCode.W,
        KeyCode.S,
        KeyCode.S,
        KeyCode.A,
        KeyCode.D,
        KeyCode.A,
        KeyCode.D,
    };
    private int sequenceIndex = 0; // 現在の入力進行度

    private void Start()
    {
        StartCoroutine(PlayTitleBGMWhenReady());

        //画面を明転させる
        //ゲームオーバー時など、タイトルに戻る際にフェードインを行うため
        FadeCanvas.instance.FadeIn(0.5f);
    }

    private void Update()
    {
        // 何らかのキーが押されたかチェック
        if (Input.anyKeyDown)
        {
            // 正しい順番のキーが押された場合
            if (Input.GetKeyDown(unlockSequence[sequenceIndex]))
            {
                sequenceIndex++;

                // 最後まで正しく入力されたら解放状態を切り替える（トグル処理）
                if (sequenceIndex >= unlockSequence.Length)
                {
                    // 現在の状態を反転させる
                    DebugMenuManager.isDebugModeUnlocked = !DebugMenuManager.isDebugModeUnlocked;

                    // 変更された状態を保存する（1なら解放、0なら未解放）
                    PlayerPrefs.SetInt(
                        "DebugModeUnlocked",
                        DebugMenuManager.isDebugModeUnlocked ? 1 : 0
                    );
                    PlayerPrefs.Save();

                    //　状態が切り替わった瞬間に、タイトル画面のUI表示を更新する
                    if (TitleUIManager.instance != null)
                    {
                        TitleUIManager.instance.UpdateDebugUI();
                    }

                    // 状態に応じてログとSEを分ける
                    if (DebugMenuManager.isDebugModeUnlocked)
                    {
                        Debug.Log(
                            "<color=cyan><b>デバッグモードが解放されました。ゲーム中にF2キーでメニューを開けます。</b></color>"
                        );

                        // // 既存のSE（例：ワープ音や決定音など）を鳴らすと分かりやすいです
                        // SEManager.instance.PlaySystemEventSE(SE_SystemEvent.Warp1);
                    }
                    else
                    {
                        Debug.Log("<color=orange><b>デバッグモードが解除されました。</b></color>");
                        // // キャンセル音など
                        // SEManager.instance.PlaySystemEventSE(SE_SystemEvent.Cancel);
                    }

                    // 入力状態をリセットして、再度コマンドを受け付けるようにする
                    sequenceIndex = 0;
                }
            }
            // マウスクリック以外の「間違ったキー」が押された場合は最初からやり直し
            else if (
                !Input.GetMouseButtonDown(0)
                && !Input.GetMouseButtonDown(1)
                && !Input.GetMouseButtonDown(2)
            )
            {
                sequenceIndex = 0;
            }
        }
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
