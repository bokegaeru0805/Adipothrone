using UnityEngine;
using UnityEngine.Playables;
using MyGame.CameraControl;

public class CameraMoveMixerBehaviour : PlayableBehaviour
{
    private CameraManager trackBinding;
    private bool firstFrame = true;

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        // 1. まずシングルトン経由で取得を試みる
        if (trackBinding == null)
        {
            trackBinding = CameraManager.instance;
        }

        // 2. 【重要】Edit Mode（プレビュー中）はinstanceがnullの場合が多いので、シーン内検索で無理やり見つける
        // これがないと、再生ボタンを押すまでTimelineのプレビューが動きません
        if (trackBinding == null && !Application.isPlaying)
        {
            trackBinding = Object.FindObjectOfType<CameraManager>();
        }

        // それでも見つからなければ何もしない
        if (trackBinding == null) return;

        int inputCount = playable.GetInputCount();
        Vector2 finalPosition = Vector2.zero; // 最終的なターゲット位置
        Vector2 weightedPosition = Vector2.zero; // 加重和
        float totalWeight = 0f; // 合計ウェイト

        // クリップの計算
        for (int i = 0; i < inputCount; i++)
        {
            float inputWeight = playable.GetInputWeight(i);
            
            if (inputWeight > 0f)
            {
                var inputPlayable = (ScriptPlayable<CameraMovePlayableBehaviour>)playable.GetInput(i);
                var input = inputPlayable.GetBehaviour();

                weightedPosition += input.targetPosition * inputWeight;
                totalWeight += inputWeight;
            }
        }

        // 制御適用
       // Timelineの影響が少しでもある場合
        if (totalWeight > 0f)
        {
            // まずはタイムライン制御モードにする
            trackBinding.SetTimelineControlMode(true);

            // 現在のカメラ位置（Brain制御下の位置）を取得したいが、
            // SetTimelineControlMode(true) した瞬間にBrainが切れるため、
            // 「Timelineの影響が0だった時の位置」または「現在のカメラ位置」を基準にする必要がある。
            
            // シンプルに「現在のカメラ座標」を取得（Zは維持）
            Vector3 currentCamPos = Camera.main.transform.position;

            // ■ パターンA：クリップ同士のブレンド（A地点→B地点）
            // totalWeightが1に近い（＝完全にTimeline支配下）なら、単純な加重平均
            // ■ パターンB：プレイヤー位置からのフェードイン
            // totalWeightが0～1の間（Ease In中）なら、元の位置とブレンドしたい

            // A地点とB地点の加重平均座標
            // totalWeightで割ることで、正規化された「ターゲット座標」を出す
            Vector2 targetPos2D = weightedPosition / totalWeight; 
            Vector3 targetPos = new Vector3(targetPos2D.x, targetPos2D.y, currentCamPos.z);

            // ★ここがポイント：
            // totalWeightが 1.0 未満（フェードイン/アウト中）の場合、
            // 「現在のカメラ位置（Timeline適用前）」と「ターゲット位置」を混ぜることは難しい。
            // なぜなら、Timeline制御ONになった瞬間に「現在のカメラ位置」はTimelineが決めなければならないから。
            
            // そのため、このMixerでは「クリップ同士のブレンド」は完璧に動作するが、
            // 「プレイヤー位置からヌルっと移動」させるには、
            // 【最初のクリップの座標をプレイヤー位置と同じにする】のが最も確実な運用方法です。

            // しかし、少しでも滑らかにするために、totalWeight自体を使って補間を行うロジックを入れることも可能ですが、
            // 挙動が不安定になりやすいため、「加重平均」のみを採用し、運用でカバーすることを推奨します。

            trackBinding.SetCameraPosition(targetPos2D);
            // Debug.Log($"[CameraMoveMixer] SetCameraPosition to {targetPos2D}, Weight:{totalWeight}, Time:{Time.time}");
        }
        else
        {
            // クリップがない区間は通常制御（Brain ON）に戻す
            trackBinding.SetTimelineControlMode(false);
            firstFrame = true;
        }
    }
    
   public override void OnGraphStop(Playable playable)
    {
        // 停止時も念のため再取得を試みてからリセット
        if (trackBinding == null) trackBinding = CameraManager.instance;
        if (trackBinding == null && !Application.isPlaying) trackBinding = Object.FindObjectOfType<CameraManager>();

        // 意図的なPause（Hold）の場合はリセット処理を行わない
        var director = playable.GetGraph().GetResolver() as PlayableDirector;
        if (director != null && 
            director.extrapolationMode == DirectorWrapMode.Hold && 
            director.state == PlayState.Paused)
        {
            // Hold設定かつ一時停止状態（＝スキップ処理によるPause）なら、
            // カメラ制御を解除せずに（現在の位置を維持したまま）抜ける
            return;
        }

        // 本当に停止（Stop）した時だけここに来る
        if (trackBinding != null)
        {
            trackBinding.SetTimelineControlMode(false);
        }
    }
}