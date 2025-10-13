using System.Collections.Generic;
using System.Threading.Tasks;
using Fungus;
using UnityEngine;

// Fungusの名前空間内にクラスを定義するか、あるいはFungus.Writerと明記します
namespace Fungus
{
    // Writerクラスを継承したCustomWriterクラスを作成
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
            base.Awake();

            var settings = SaveLoadManager.instance?.Settings;
            if (settings == null)
            {
                Debug.LogError("設定用のデータががありません。", this);
                return;
            }
            
            writingSpeed = speedToCharPerSecond[settings.messageSpeed]; // 1秒あたりの文字数を設定
        }
    }
}
