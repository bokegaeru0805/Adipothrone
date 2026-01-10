using UnityEngine;
using UnityEngine.Playables;

public class WarpMixerBehaviour : PlayableBehaviour
{
    // Trackでバインドされたオブジェクト（キャラクター）
    private Transform trackBinding;
    private Rigidbody2D rb;

    // 初期位置を保存するための変数
    private Vector3 initialPosition;
    private bool hasCapturedInitialPosition = false;

    // Timeline再生開始時に、操作対象を取得する
    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        // バインド対象（キャラ）を取得。なければ何もしない
        trackBinding = playerData as Transform;
        if (trackBinding == null)
            return;

        // Rigidbody2Dのキャッシュ（物理演算用）
        if (rb == null)
            rb = trackBinding.GetComponent<Rigidbody2D>();

        // まだ初期位置を保存していなければ保存する
        // ※OnGraphStartではなくここでやるのは、trackBinding(playerData)が確実に取得できるため
        if (!hasCapturedInitialPosition)
        {
            initialPosition = trackBinding.position;
            hasCapturedInitialPosition = true;
        }

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
                Vector3 newPos = new Vector3(
                    input.targetPosition.x,
                    input.targetPosition.y,
                    trackBinding.position.z
                );

                // 物理挙動がある場合はRigidbody経由、なければTransform経由で移動
                // 物理挙動がある場合はRigidbody経由
                if (Application.isPlaying && rb != null)
                {
                    // 移動前の慣性（速度）を完全に殺す
                    // これがないと、Warpした瞬間に以前の移動速度で吹っ飛ぶことがあります
                    rb.velocity = Vector2.zero;
                    
                    // RigidbodyとTransformの両方を更新する（念には念を）
                    // Rigidbodyの更新は次の物理フレームまで反映されないことがあるため、
                    // 見た目のズレを防ぐためにTransformも強制一致させます。
                    rb.transform.position = newPos; 
                    rb.position = newPos; 
                }
                else
                {
                    trackBinding.position = newPos; // エディタ編集用、または物理なし用
                }

                //Debug.Log($"[WarpMixerBehaviour] {trackBinding.name} moved to {newPos}");

                // 1つのクリップが見つかったら、他のクリップは無視して終了
                return;
            }
        }
    }

    public override void OnGraphStop(Playable playable)
    {
        // エディタでのプレビュー中（非再生中）のみ、位置を元に戻す
        // これにより、ゲーム中は「移動しっぱなし」になり、Timeline終了後もその場に留まることができます。
        if (!Application.isPlaying)
        {
            if (hasCapturedInitialPosition && trackBinding != null)
            {
                trackBinding.position = initialPosition;
            }
        }

        // ゲーム中（Application.isPlaying == true）は何もしない＝Warp先に留まる

        // フラグをリセット（次回再生時のため）
        hasCapturedInitialPosition = false;
    }
}
