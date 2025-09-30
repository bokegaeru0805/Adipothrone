using System.Collections;
using CriWare;
using CriWare.Assets;
using UnityEngine;

public class Debug_CriBgmPlayer : MonoBehaviour
{
    [SerializeField]
    private CriAtomCueReference cueRef;
    CriAtomExPlayer BGMPlayer;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        //プレーヤー作成
        BGMPlayer = new CriAtomExPlayer();

        PlaySound(cueRef);
    }

    /// <summary>
    /// 任意のCueNameの内容を再生
    /// 再生内容は適宜再生元で設定したCriAtomCueReferenceを引数として参照し、再生します。
    /// </summary>
    public void PlaySound(CriAtomCueReference cueRef)
    {
        //もし再生中であれば停止
        if (BGMPlayer.GetStatus() == CriAtomExPlayer.Status.Playing)
        {
            BGMPlayer.Stop();
        }

        BGMPlayer.SetCue(cueRef.AcbAsset.Handle, cueRef.CueId);
        BGMPlayer.Start();
    }

    [Header("初期再生BGM")]
    public string cueSheetName = "BGMSheet";
    public string cueName = "PlainsField";

    // クロスフェードで切り替えるBGMの情報をインスペクターで設定
    [Header("クロスフェード先のBGM")]
    public string crossfadeCueName = "UniqueBoss";

    [Header("クロスフェード時間（秒）")]
    public float fadeDuration = 2.0f;

    // プレイヤーを2つ用意
    private CriAtomExPlayer player1;
    private CriAtomExPlayer player2;
    private CriAtomExPlayer currentPlayer; // 現在メインで再生しているプレイヤー

    private CriAtomExAcb bgmAcb;
    private float duckingLevel = 0.0f;

    [SerializeField]
    private AudioSource audioSource;

    // void Start()
    // {
    //     // プレイヤーを2つ生成
    //     player1 = new CriAtomExPlayer();
    //     player2 = new CriAtomExPlayer();
    //     // 最初はplayer1をメインプレイヤーとして設定
    //     currentPlayer = player1;
    // }

    // void OnDestroy()
    // {
    //     if (player1 != null)
    //     {
    //         player1.Dispose();
    //         player1 = null;
    //     }
    //     if (player2 != null)
    //     {
    //         player2.Dispose();
    //         player2 = null;
    //     }
    //     if (bgmAcb != null)
    //     {
    //         bgmAcb.Dispose();
    //         bgmAcb = null;
    //     }
    // }

    // /// <summary>
    // /// BGMの再生を開始します (初期再生用)
    // /// </summary>
    // public void PlayInitialBGM()
    // {
    //     if (string.IsNullOrEmpty(cueSheetName) || string.IsNullOrEmpty(cueName))
    //     {
    //         Debug.LogError("キューシート名またはキュー名が設定されていません。");
    //         return;
    //     }

    //     bgmAcb = CriAtomExAcb.LoadAcbFile(null, cueSheetName + ".acb", cueSheetName + ".awb");

    //     currentPlayer.SetCue(bgmAcb, cueName);
    //     currentPlayer.Start();
    //     Debug.Log($"BGM Play Start: {cueSheetName} -> {cueName}");
    // }

    // /// <summary>
    // /// 現在再生中のBGMを停止します
    // /// </summary>
    // public void Stop()
    // {
    //     currentPlayer.Stop();
    //     Debug.Log("BGM Stop");
    // }

    // // クロスフェード処理を行うコルーチン
    // private IEnumerator CrossfadeCoroutine(string newCueSheet, string newCue, float duration)
    // {
    //     // 1. メインではない方のプレイヤーを特定する
    //     CriAtomExPlayer fadeInPlayer = (currentPlayer == player1) ? player2 : player1;
    //     CriAtomExPlayer fadeOutPlayer = currentPlayer;

    //     // 2. 新しい曲を再生準備し、音量0で再生開始
    //     var newAcb = CriAtomExAcb.LoadAcbFile(null, newCueSheet + ".acb", newCueSheet + ".awb");
    //     fadeInPlayer.SetCue(newAcb, newCue);
    //     fadeInPlayer.SetVolume(0.0f);
    //     fadeInPlayer.Start();
    //     fadeOutPlayer.SetVolume(1.0f); // 念のため、フェードアウト側の音量を最大に設定

    //     Debug.Log($"Crossfade Start: to {newCue}");

    //     // 3. 指定した時間をかけて音量を滑らかに変化させる
    //     float timer = 0f;
    //     while (timer < duration)
    //     {
    //         timer += Time.deltaTime;
    //         float progress = timer / duration;

    //         fadeOutPlayer.SetVolume(1.0f - progress); // 現在の曲をフェードアウト
    //         fadeInPlayer.SetVolume(progress); // 新しい曲をフェードイン

    //         // プレイヤーの状態を更新してボリューム変更を即座に反映
    //         fadeOutPlayer.UpdateAll();
    //         fadeInPlayer.UpdateAll();

    //         yield return null; // 次のフレームまで待機
    //     }

    //     // 4. 処理の完了
    //     fadeOutPlayer.Stop(); // 古い曲を完全に停止
    //     fadeInPlayer.SetVolume(1.0f); // 新しい曲の音量を最大に
    //     fadeInPlayer.UpdateAll();

    //     currentPlayer = fadeInPlayer; // メインプレイヤーを入れ替え

    //     Debug.Log("Crossfade Complete");
    // }

    // private void Update()
    // {
    //     // マウスの左ボタンで最初の曲を再生
    //     if (Input.GetMouseButtonDown(0))
    //     {
    //         PlayInitialBGM();
    //     }

    //     // Aキーでクロスフェードを開始
    //     if (Input.GetKeyDown(KeyCode.A))
    //     {
    //         StartCoroutine(
    //             CrossfadeCoroutine(cueSheetName, crossfadeCueName, fadeDuration)
    //         );
    //     }

    //     if (Input.GetMouseButtonDown(1))
    //     {
    //         audioSource.Play();
    //         Debug.Log("AudioSource Play");
    //     }


    //     // // マウスホイールでAISACを操作 (現在のメインプレイヤーに対して)
    //     // float scrollInput = Input.GetAxis("Mouse ScrollWheel");
    //     // if (scrollInput != 0)
    //     // {
    //     //     duckingLevel = Mathf.Clamp01(duckingLevel + scrollInput * 3f);
    //     //     currentPlayer.SetAisacControl("DuckingControl", duckingLevel);
    //     //     currentPlayer.UpdateAll();
    //     //     Debug.Log("Ducking Level: " + duckingLevel);
    //     // }
    // }
}