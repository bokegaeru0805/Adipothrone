using System.Collections.Generic;
using UnityEngine;

namespace Fungus
{
    public class CustomWriter : Writer
    {
        private Dictionary<MessageSpeed, float> speedToCharPerSecond = new Dictionary<
            MessageSpeed,
            float
        >
        {
            { MessageSpeed.Slow, 30f },
            { MessageSpeed.Normal, 60f },
            { MessageSpeed.Fast, 90f },
            { MessageSpeed.VeryFast, 120f },
        };

        protected override void Awake()
        {
            if (!GameManager.isFirstGameSceneOpen)
                return; // ゲームが開始されていない場合は何もしない

            base.Awake();

            var settings = SaveLoadManager.instance?.Settings;
            if (settings == null)
            {
                Debug.LogError("設定用のデータががありません。", this);
                return;
            }

            writingSpeed = speedToCharPerSecond[settings.messageSpeed]; // 1秒あたりの文字数を設定
        }

        
        /// <summary>
        /// メッセージの表示を強制的に進める
        ///  Writerの内部フラグを操作して、クリックされたことにする
        /// </summary>
        public void ForceAdvance()
        {
            // Writerの内部フラグを操作して、クリックされたことにする
            this.inputFlag = true;
        }
    }
}
