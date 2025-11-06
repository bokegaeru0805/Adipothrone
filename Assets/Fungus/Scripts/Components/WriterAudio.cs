// This code is part of the Fungus library (https://github.com/snozbot/fungus)
// It is released for free under the MIT open source license (https://github.com/snozbot/fungus/blob/master/LICENSE)

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Fungus
{
    /// <summary>
    /// Type of audio effect to play.
    /// </summary>
    public enum AudioMode
    {
        /// <summary> Use short beep sound effects. </summary>
        Beeps,

        /// <summary> Use long looping sound effect. </summary>
        SoundEffect,
    }

    /// <summary>
    /// Manages audio effects for Dialogs. (SEManager / CRIWARE Edition)
    /// </summary>
    public class WriterAudio : MonoBehaviour, IWriterListener
    {
        public float GetSecondsRemaining()
        {
            // SEManager (CRI) はAudioClipの残り時間を直接取得できません
            return 0f;
        }

        protected virtual void SetAudioMode(AudioMode mode)
        {
            // "SetAudioMode_CustomWriterAudio" という名前の関数を、同じゲームオブジェクト上の全コンポーネントから探して実行
            SendMessage(
                "SetAudioMode_CustomWriterAudio",
                mode,
                SendMessageOptions.DontRequireReceiver
            );
        }

        protected virtual void Awake()
        {
            // targetAudioSourceの初期化処理を削除
        }

        protected virtual void Play(AudioClip audioClip)
        {
            // "Play_CustomWriterAudio" という名前の関数を、同じゲームオブジェクト上の全コンポーネントから探して実行
            SendMessage("Play_CustomWriterAudio", SendMessageOptions.DontRequireReceiver);
        }

        protected virtual void Pause()
        {
            SendMessage("Pause_CustomWriterAudio", SendMessageOptions.DontRequireReceiver);
        }

        protected virtual void Stop()
        {
            SendMessage("Stop_CustomWriterAudio", SendMessageOptions.DontRequireReceiver);
        }

        protected virtual void Resume()
        {
            // "Resume_CustomWriterAudio" という名前の関数を、同じゲームオブジェクト上の全コンポーネントから探して実行
            SendMessage("Resume_CustomWriterAudio", SendMessageOptions.DontRequireReceiver);
        }

        protected virtual void Update()
        {
            // AudioSourceの音量フェード処理を削除
        }

        #region IWriterListener implementation

        public virtual void OnInput()
        {
            // "OnInput_CustomWriterAudio" という名前の関数を、同じゲームオブジェクト上の全コンポーネントから探して実行
            SendMessage("OnInput_CustomWriterAudio", SendMessageOptions.DontRequireReceiver);
        }

        public virtual void OnStart(AudioClip audioClip)
        {
            Play(audioClip);
        }

        public virtual void OnPause()
        {
            Pause();
        }

        public virtual void OnResume()
        {
            Resume();
        }

        public virtual void OnEnd(bool stopAudio)
        {
            if (stopAudio)
            {
                Stop();
            }
        }

        public virtual void OnGlyph()
        { // "OnGlyph_CustomWriterAudio" という名前の関数を、同じゲームオブジェクト上の全コンポーネントから探して実行
            SendMessage("OnGlyph_CustomWriterAudio", SendMessageOptions.DontRequireReceiver);
        }

        public virtual void OnVoiceover(AudioClip voiceOverClip)
        {
            // "OnVoiceover_CustomWriterAudio" という名前の関数を、同じゲームオブジェクト上の全コンポーネントから探して実行
            SendMessage(
                "OnVoiceover_CustomWriterAudio",
                voiceOverClip,
                SendMessageOptions.DontRequireReceiver
            );
        }

        public void OnAllWordsWritten() { }

        #endregion
    }
}
