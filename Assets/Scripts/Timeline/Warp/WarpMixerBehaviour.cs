using UnityEngine;
using UnityEngine.Playables;

public class WarpMixerBehaviour : PlayableBehaviour
{
    // Trackでバインドされたオブジェクト（キャラクター）
    private Transform trackBinding;
    private Rigidbody2D rb;

    // Timeline再生開始時に、操作対象を取得する
    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        // バインド対象（キャラ）を取得。なければ何もしない
        trackBinding = playerData as Transform;
        if (trackBinding == null) return;

        // Rigidbody2Dのキャッシュ（物理演算用）
        if (rb == null) rb = trackBinding.GetComponent<Rigidbody2D>();

        int inputCount = playable.GetInputCount();

        for (int i = 0; i < inputCount; i++)
        {
            float inputWeight = playable.GetInputWeight(i);
            
            // クリップの上にバーがある（Weightが少しでもある）時だけ実行
            if (inputWeight > 0f)
            {
                var inputPlayable = (ScriptPlayable<WarpPlayableBehaviour>)playable.GetInput(i);
                WarpPlayableBehaviour input = inputPlayable.GetBehaviour();

                // 現在のZ座標を維持しつつ、XYを指定座標に移動
                Vector3 newPos = new Vector3(input.targetPosition.x, input.targetPosition.y, trackBinding.position.z);

                // 物理挙動がある場合はRigidbody経由、なければTransform経由で移動
                if (Application.isPlaying && rb != null)
                {
                    rb.position = newPos; // 物理演算を壊さない移動
                }
                else
                {
                    trackBinding.position = newPos; // エディタ編集用、または物理なし用
                }
                
                // 1つのクリップが見つかったら、他のクリップは無視して終了（重なり非推奨）
                return;
            }
        }
    }
}