using System;
using System.Collections.Generic;
using Fungus;
using UnityEngine;

/// <summary>
/// Manages audio effects for Dialogs. (SEManager / CRIWARE Edition)
/// </summary>
[RequireComponent(typeof(CriWare.Assets.CriAtomSePlayer))]
public class CustomWriterAudio : MonoBehaviour
{
    // [Tooltip("Volume level of writing sound effects")]
    // [Range(0,1)]
    // [SerializeField] protected float volume = 1f; // SEManagerがグローバル音量を管理するため削除

    // [Tooltip("Loop the audio when in Sound Effect mode. Has no effect in Beeps mode.")]
    // [SerializeField] protected bool loop = true; // CRI AtomCraft側で設定するため削除

    // [Tooltip("AudioSource to use for playing sound effects. If none is selected then one will be created.")]
    // [SerializeField] protected AudioSource targetAudioSource; // SEManagerを使うため削除

    [Tooltip("Type of sound effect to play when writing text")]
    [SerializeField]
    protected AudioMode audioMode = AudioMode.Beeps;

    //--- SEManager用に変更 ---
    //SEManagerがSE_UI以外のenumを使う場合は、以下のフィールドの型を変更してください。

    [Tooltip("List of beeps (CRI Cues) to randomly select when playing beep sound effects.")]
    [SerializeField]
    protected List<SE_UI> beepCues = new List<SE_UI>();

    [Tooltip(
        "Long playing sound effect (CRI Cue) to play when writing text. Must be set to loop in CRI AtomCraft."
    )]
    [SerializeField]
    protected SE_UI soundEffectCue;

    [Tooltip("Sound effect (CRI Cue) to play on user input (e.g. a click)")]
    [SerializeField]
    protected SE_UI inputSoundCue;

    // protected float targetVolume = 0f; // 音量フェードを削除

    // When true, a beep will be played on every written character glyph
    protected bool playBeeps;

    // True when a voiceover clip is playing
    protected bool playingVoiceover = false;
    private CriWare.Assets.CriAtomSePlayer sePlayer;

    public bool IsPlayingVoiceOver
    {
        get { return playingVoiceover; }
    }

    // protected float nextBeepTime; // 再生間隔の制御を削除

    /// <summary>
    /// Enumが 0 (None) かどうかをチェックするヘルパー
    /// </summary>
    private bool IsCueNone(Enum cue)
    {
        if (cue == null)
            return true;
        try
        {
            // Enumを基底の整数型に変換して0と比較
            return Convert.ToInt64(cue) == 0;
        }
        catch
        {
            return true; // 変換失敗時はNone扱い
        }
    }

    public float GetSecondsRemaining()
    {
        // SEManager (CRI) はAudioClipの残り時間を直接取得できません
        return 0f;
    }

    public virtual void SetAudioMode_CustomWriterAudio(AudioMode mode)
    {
        audioMode = mode;
    }

    protected virtual void Awake()
    {
        sePlayer = GetComponent<CriWare.Assets.CriAtomSePlayer>();
        // targetAudioSourceの初期化処理を削除
    }

    protected virtual void Play_CustomWriterAudio()
    {
        // SEManagerはAudioClipを再生できません。
        // audioClip (ボイスオーバーまたはダイアログ固有SE) は無視されます。
        playingVoiceover = false;

        if (audioMode == AudioMode.SoundEffect && !IsCueNone(soundEffectCue))
        {
            // ループ再生はCRI AtomCraft側で設定する必要があります
            sePlayer.Play(soundEffectCue);
        }
        else if (audioMode == AudioMode.Beeps && beepCues.Count > 0)
        {
            // Beepsモードの場合、OnGlyphで再生する
            playBeeps = true;
        }
    }

    protected virtual void Pause()
    {
        sePlayer.player.Pause();
    }

    protected virtual void Stop()
    {
        sePlayer.player.Stop();
        playBeeps = false;
        playingVoiceover = false;
    }

    protected virtual void Resume_CustomWriterAudio()
    {
        if (audioMode == AudioMode.SoundEffect && !IsCueNone(soundEffectCue))
        {
            // Resume = Play again
            sePlayer.Play(soundEffectCue);
        }
    }

    protected virtual void Update()
    {
        // AudioSourceの音量フェード処理を削除
    }

    #region IWriterListener implementation

    public virtual void OnInput_CustomWriterAudio()
    {
        if (!IsCueNone(inputSoundCue))
        {
            sePlayer.Play(inputSoundCue);
        }
    }

    public virtual void OnStart_CustomWriterAudio(AudioClip audioClip)
    {
        if (playingVoiceover)
        {
            return;
        }
        Play_CustomWriterAudio();
    }

    public virtual void OnPause_CustomWriterAudio()
    {
        if (playingVoiceover)
        {
            return;
        }
        Pause();
    }

    public virtual void OnResume_CustomWriterAudio()
    {
        if (playingVoiceover)
        {
            return;
        }
        Resume_CustomWriterAudio();
    }

    public virtual void OnEnd_CustomWriterAudio(bool stopAudio)
    {
        if (stopAudio)
        {
            Stop();
        }
    }

    public virtual void OnGlyph_CustomWriterAudio()
    {
        if (playingVoiceover)
        {
            return;
        }

        if (playBeeps && beepCues.Count > 0)
        {
            // 元のAudioSource.isPlayingチェックやnextBeepTimeによるレート制限は、
            // SEManagerの構造上、簡潔に実装できないため省略します。
            // 1文字ごとに再生リクエストが発行されます。
            // Cuesheet側で同時再生数を制限するなどの対応を推奨します。

            SE_UI cueToPlay = beepCues[UnityEngine.Random.Range(0, beepCues.Count)];
            if (!IsCueNone(cueToPlay))
            {
                sePlayer.Play(cueToPlay);
            }
        }
    }

    public virtual void OnVoiceover_CustomWriterAudio(AudioClip voiceOverClip)
    {
        if (voiceOverClip == null)
        {
            return;
        }

        // SEManagerはAudioClipを再生できません。
        // このスクリプトはビープ音を停止する役割のみ担います。
        // 実際のボイス再生は、別のCRIWARE用システム (VIManagerなど) が
        // FungusのVoiceoverイベントをリッスンして行う必要があります。

        // 現在のビープ音やSEを停止
        Stop();

        // ボイスオーバー再生中フラグを立て、ビープ音が鳴らないようにする
        playingVoiceover = true;
    }

    public void OnAllWordsWritten() { }

    #endregion
}
