using System;
using System.Collections.Generic;
using CriWare;
using CriWare.Assets;
using UnityEngine;

/// <summary>
/// SEの再生を統括管理するマネージャクラス（CRI ADX2版）
/// </summary>
public class SEManager : MonoBehaviour
{
    #region 変数宣言

    [Header("SEのACBアセット")]
    [SerializeField]
    private CriAtomAcbAsset seAcbAsset;

    /// <summary>
    /// シングルトンインスタンス
    /// </summary>
    public static SEManager instance { get; private set; }

    /// <summary>
    /// SEを再生するための共通プレイヤー
    /// </summary>
    private CriAtomExPlayer sePlayer;

    private const string SE_CATEGORY_NAME = "SE"; // SEカテゴリのパラメータ名

    /// <summary>
    /// Timeline操作中などにSEをミュートするためのフラグ
    /// </summary>
    public bool IsTimelineMuted { get; set; } = false;

    /// <summary>
    /// 再生中のSEを個別に追跡するための辞書。
    /// 同じSEが重なって複数回再生されることを考慮し、Listで管理します。
    /// </summary>
    private Dictionary<Enum, List<CriAtomExPlayback>> activePlaybacks =
        new Dictionary<Enum, List<CriAtomExPlayback>>();

    /// <summary>
    /// 再生終了したSEを辞書から削除する処理（お掃除）を実行する間隔（秒）
    /// </summary>
    private const float CLEANUP_INTERVAL = 1.0f;

    /// <summary>
    /// お掃除用のタイマー
    /// </summary>
    private float cleanupTimer = 0f;

    #endregion


    #region Unityライフサイクル

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            //DontDestroyOnLoad(this.gameObject);
        }
        else
        {
            Destroy(this.gameObject);
        }

        if (seAcbAsset == null)
        {
            Debug.LogError("SEManager: SEのACBアセットが設定されていません。");
        }
    }

    private void Start()
    {
        sePlayer = new CriAtomExPlayer();

        // Atom Craft側が3Dポジショニング設定でも、このプレイヤーでは常に通常のパン（Pan3d＝2D的な定位）として強制再生し、リスナー未設定エラーを防ぐ
        sePlayer.SetPanType(CriAtomEx.PanType.Pan3d);

        // SEの音量を設定
        if (SaveLoadManager.instance != null)
        {
            float seVolume = SaveLoadManager.instance.Settings.seVolume;
            AdjustAllSEVolume(seVolume);
        }
        else
        {
            Debug.LogError("SaveLoadManagerが見つかりません。");
        }
    }

    private void Update()
    {
        // 毎フレームではなく、一定時間ごとに辞書のお掃除（削除）を実行して負荷を減らす
        cleanupTimer += Time.deltaTime;
        if (cleanupTimer >= CLEANUP_INTERVAL)
        {
            CleanupPlaybacks();
            cleanupTimer = 0f; // タイマーをリセット
        }
    }

    #endregion


    #region 内部管理メソッド（Playbackの追跡・クリーンアップ）

    /// <summary>
    /// 再生開始時にPlayback情報（再生実体）を辞書に登録する補助メソッド
    /// </summary>
    private void RegisterPlayback(Enum cue, CriAtomExPlayback playback)
    {
        // まだそのSEのリストが存在しなければ、新しく作成する
        if (!activePlaybacks.ContainsKey(cue))
        {
            activePlaybacks[cue] = new List<CriAtomExPlayback>();
        }
        activePlaybacks[cue].Add(playback);
    }

    /// <summary>
    /// 再生終了したPlaybackを辞書から自動削除する管理メソッド
    /// </summary>
    private void CleanupPlaybacks()
    {
        List<Enum> keysToRemove = new List<Enum>();

        foreach (var kvp in activePlaybacks)
        {
            var playbacks = kvp.Value;

            // リストから要素を削除するため、インデックスの後ろから前へ向かってループ処理します
            // （前から消すとインデックスがズレてエラーになるため）
            for (int i = playbacks.Count - 1; i >= 0; i--)
            {
                CriAtomExPlayback.Status status = playbacks[i].GetStatus();
                // 再生中(Playing)または再生準備中(Prep)でなければ、終了したとみなしてリストから削除
                if (
                    status != CriAtomExPlayback.Status.Playing
                    && status != CriAtomExPlayback.Status.Prep
                )
                {
                    playbacks.RemoveAt(i);
                }
            }

            // Playbackリストが空になったら、そのEnumキー自体を辞書から削除する候補に入れる
            if (playbacks.Count == 0)
            {
                keysToRemove.Add(kvp.Key);
            }
        }

        // 空になったキーを辞書から完全に削除する
        foreach (var key in keysToRemove)
        {
            activePlaybacks.Remove(key);
        }
    }

    #endregion


    #region SE再生メソッド (Play)

    /// <summary>
    /// パラメータ指定付きでSEを再生（Timeline用）
    /// </summary>
    public void PlayEx(Enum cue, bool useVolume, float volume, bool usePitch, float pitch)
    {
        // ミュート中は何もせず戻る
        if (IsTimelineMuted)
        {
            // Debug.Log($"SEManager: Timelineがミュート中のため、SE '{cue}' の再生をスキップ。");
            return;
        }

        string cueName = SeCueDatabase.GetCueName(cue);
        if (cueName == null)
        {
            Debug.LogWarning(
                $"SEManager: 指定されたSE enum '{cue}' に対応するキュー名が見つかりません。"
            );
            return;
        }

        // 1. パラメータ設定
        // 指定がある場合だけセットし、なければデフォルト(Vol=1.0, Pitch=0)に戻すなどの運用が安全ですが、
        // ADX2の仕様上、前回の設定が残る可能性があるため、明示的にセットします。

        // ボリューム設定（指定がなければ、現在のカテゴリボリュームなどは触らずPlayerの倍率を1.0に戻す）
        sePlayer.SetVolume(useVolume ? volume : 1.0f);

        // ピッチ設定（指定がなければ0に戻す）
        sePlayer.SetPitch(usePitch ? pitch : 0f);

        // 2. 再生
        sePlayer.SetCue(seAcbAsset.Handle, cueName);
        CriAtomExPlayback playback = sePlayer.Start();

        // 個別の再生情報を辞書に登録
        RegisterPlayback(cue, playback);

        // 3. 次回の再生に影響が出ないよう、パラメータをリセットしておく（安全策）
        // ※ただしStart直後のResetは反映タイミングに注意が必要ですが、ADX2はStart時点のパラメータが使われるため基本OK
        // ここでは「次にPlay(Enum)だけ呼ばれた時」のためにリセットは行わず、
        // 常にSetVolume/SetPitchを行う運用にするか、以下のようにリセットを入れるか選択になります。
        // 今回は安全のため、次のフレーム以降のためにリセットしておきます。
        // sePlayer.SetVolume(1.0f);
        // sePlayer.SetPitch(0f);
    }

    /// <summary>
    /// 指定されたenumに対応するSEを再生します。どのカテゴリのenumでも受け付けます。
    /// </summary>
    /// <param name="cue">再生したいSEのenum（例：SE_UI.Decision1）</param>
    public void Play(Enum cue)
    {
        // ミュート中は何もせず戻る
        if (IsTimelineMuted)
            return;

        // 辞書からキュー名（string）を取得
        string cueName = SeCueDatabase.GetCueName(cue);
        if (cueName == null)
        {
            Debug.LogWarning(
                $"SEManager: 指定されたSE enum '{cue}' に対応するキュー名が見つかりません。"
            );
            return;
        }

        sePlayer.SetCue(seAcbAsset.Handle, cueName);
        CriAtomExPlayback playback = sePlayer.Start();

        // 個別の再生情報を辞書に登録
        RegisterPlayback(cue, playback);
    }

    /// <summary>UI系のSEを再生</summary>
    public void PlayUISE(SE_UI se)
    {
        Play(se);
    }

    /// <summary>プレイヤーアクション系のSEを再生</summary>
    public void PlayPlayerActionSE(SE_PlayerAction se)
    {
        Play(se);
    }

    /// <summary>敵アクション系のSEを再生</summary>
    public void PlayEnemyActionSE(SE_EnemyAction se)
    {
        Play(se);
    }

    /// <summary>環境・ギミック系のSEを再生</summary>
    public void PlayFieldSE(SE_Field se)
    {
        Play(se);
    }

    /// <summary>システムイベント系のSEを再生</summary>
    public void PlaySystemEventSE(SE_SystemEvent se)
    {
        Play(se);
    }

    #endregion


    #region SEピッチ変更再生メソッド (Play Pitch)

    /// <summary>
    /// 指定したSEをピッチを変更して再生するメソッド（共通処理）
    /// </summary>
    private void PlayWithPitch(Enum cue, float pitch)
    {
        if (IsTimelineMuted)
            return;

        string cueName = SeCueDatabase.GetCueName(cue);
        if (cueName == null)
        {
            Debug.LogWarning(
                $"SEManager: 指定されたSE enum '{cue}' に対応するキュー名が見つかりません。"
            );
            return;
        }

        sePlayer.SetPitch(pitch);
        sePlayer.SetCue(seAcbAsset.Handle, cueName);
        CriAtomExPlayback playback = sePlayer.Start();

        RegisterPlayback(cue, playback);

        // 他の通常再生に影響を与えないようにピッチをリセットしておく
        sePlayer.SetPitch(0f);
    }

    /// <summary>UI系のSEをピッチ変更して再生</summary>
    public void PlayUISEPitch(SE_UI se, float pitch)
    {
        PlayWithPitch(se, pitch);
    }

    /// <summary>プレイヤーアクション系のSEをピッチ変更して再生</summary>
    public void PlayPlayerActionSEPitch(SE_PlayerAction se, float pitch)
    {
        PlayWithPitch(se, pitch);
    }

    /// <summary>敵アクション系のSEをピッチ変更して再生</summary>
    public void PlayEnemyActionSEPitch(SE_EnemyAction se, float pitch)
    {
        PlayWithPitch(se, pitch);
    }

    /// <summary>環境・ギミック系のSEをピッチ変更して再生</summary>
    public void PlayFieldSEPitch(SE_Field se, float pitch)
    {
        PlayWithPitch(se, pitch);
    }

    /// <summary>システムイベント系のSEをピッチ変更して再生</summary>
    public void PlaySystemEventSEPitch(SE_SystemEvent se, float pitch)
    {
        PlayWithPitch(se, pitch);
    }

    #endregion


    #region SE停止メソッド (Stop)

    /// <summary>
    /// 指定されたEnumに対応する再生中のSEのみを停止します。
    /// </summary>
    private void StopSE(Enum cue)
    {
        // 指定されたSEが辞書に存在すれば、そのPlaybackを全て停止させる
        if (activePlaybacks.TryGetValue(cue, out List<CriAtomExPlayback> playbacks))
        {
            foreach (var playback in playbacks)
            {
                playback.Stop();
            }
            // 停止させたので辞書から削除
            activePlaybacks.Remove(cue);
        }
        else
        {
            // 再生されていない場合は特に何もしない
            // Debug.Log($"SEManager: 指定されたSE enum '{cue}' は現在再生されていません。");
        }
    }

    /// <summary>
    /// 指定されたEnumに対応する再生中のSEのみを停止します。
    /// immediate が false の場合は、Atom Craftで設定したリリース時間（フェードアウト）に従います。
    /// true の場合は、フェードアウトを無視して即座に音が消えます。
    /// </summary>
    public void StopEx(Enum cue, bool immediate = false)
    {
        // 指定されたSEが辞書に存在すれば、そのPlaybackを全て停止させる
        if (activePlaybacks.TryGetValue(cue, out List<CriAtomExPlayback> playbacks))
        {
            foreach (var playback in playbacks)
            {
                // immediateがfalseならフェードアウト（リリース）あり、trueなら即時停止
                playback.Stop(immediate);
            }
            // 停止させたので辞書から削除
            activePlaybacks.Remove(cue);
        }
    }

    /// <summary>UI系のSEを停止</summary>
    public void StopUISE(SE_UI se)
    {
        StopSE(se);
    }

    /// <summary>プレイヤーアクション系のSEを停止</summary>
    public void StopPlayerActionSE(SE_PlayerAction se)
    {
        StopSE(se);
    }

    /// <summary>敵アクション系のSEを停止</summary>
    public void StopEnemyActionSE(SE_EnemyAction se)
    {
        StopSE(se);
    }

    /// <summary>環境・ギミック系のSEを停止</summary>
    public void StopFieldSE(SE_Field se)
    {
        StopSE(se);
    }

    /// <summary>システムイベント系のSEを停止</summary>
    public void StopSystemEventSE(SE_SystemEvent se)
    {
        StopSE(se);
    }

    #endregion


    #region SE再生状態確認メソッド (IsPlaying)

    /// <summary>
    /// 指定されたEnumに対応するSEが現在再生中かどうかを判定します。
    /// </summary>
    private bool IsPlaying(Enum cue)
    {
        // 辞書にキーが存在し、かつ状態がPlaying（またはPrep）のものが1つでもあればtrue
        if (activePlaybacks.TryGetValue(cue, out List<CriAtomExPlayback> playbacks))
        {
            foreach (var playback in playbacks)
            {
                CriAtomExPlayback.Status status = playback.GetStatus();
                if (
                    status == CriAtomExPlayback.Status.Playing
                    || status == CriAtomExPlayback.Status.Prep
                )
                {
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>指定したUI系SEが再生中か判定</summary>
    public bool IsPlayingUISE(SE_UI se)
    {
        return IsPlaying(se);
    }

    /// <summary>指定したプレイヤーアクション系SEが再生中か判定</summary>
    public bool IsPlayingPlayerActionSE(SE_PlayerAction se)
    {
        return IsPlaying(se);
    }

    /// <summary>指定した敵アクション系SEが再生中か判定</summary>
    public bool IsPlayingEnemyActionSE(SE_EnemyAction se)
    {
        return IsPlaying(se);
    }

    /// <summary>指定した環境・ギミック系SEが再生中か判定</summary>
    public bool IsPlayingFieldSE(SE_Field se)
    {
        return IsPlaying(se);
    }

    /// <summary>指定したシステムイベント系SEが再生中か判定</summary>
    public bool IsPlayingSystemEventSE(SE_SystemEvent se)
    {
        return IsPlaying(se);
    }

    #endregion


    #region 全体制御メソッド (Volume / StopAll)

    /// <summary>
    /// 再生中のすべてのSEを強制的に停止します。
    /// </summary>
    public void StopAllSE()
    {
        if (sePlayer != null)
        {
            sePlayer.Dispose();
            sePlayer = new CriAtomExPlayer();

            // プレイヤーを再生成した際も、常に通常のパン（Pan3d）として強制再生する
            sePlayer.SetPanType(CriAtomEx.PanType.Pan3d);
        }

        // 追跡している辞書データもリセット
        activePlaybacks.Clear();
    }

    /// <summary>
    /// すべてのSEの音量を調整する
    /// </summary>
    public void AdjustAllSEVolume(float ratio)
    {
        CriAtom.SetCategoryVolume(SE_CATEGORY_NAME, ratio);
    }

    /// <summary>
    /// 現在のSEの全体音量を取得します
    /// </summary>
    public float GetAllVolume()
    {
        return CriAtom.GetCategoryVolume(SE_CATEGORY_NAME);
    }

    #endregion
}
