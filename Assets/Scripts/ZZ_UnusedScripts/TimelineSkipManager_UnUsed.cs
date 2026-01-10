// using UnityEngine;
// using UnityEngine.Playables;

// /// <summary>
// /// シーン内のTimeline再生を一括管理し、早送りやスキップ機能を提供するマネージャクラス。
// /// シングルトンとして動作し、現在再生中のPlayableDirectorを保持します。
// /// </summary>
// public class TimelineSkipManager : MonoBehaviour
// {
//     /// <summary>
//     /// シングルトンインスタンス
//     /// </summary>
//     public static TimelineSkipManager instance { get; private set; }

//     [Header("設定")]
//     [Tooltip("早送り時の倍率（例: 3.0なら3倍速）")]
//     [SerializeField]
//     private float fastForwardSpeed = 3.0f;

//     // 現在アクティブな（再生中の）Directorへの参照
//     private PlayableDirector activeDirector;

//     // Timeline本来の再生速度（早送り解除時に戻すため保持）
//     private double defaultSpeed = 1.0;

//     // 現在早送り中かどうかのフラグ
//     private bool isFastForwarding = false;

//     private void Awake()
//     {
//         // シングルトン設定
//         if (instance == null)
//         {
//             instance = this;
//             // シーン遷移後も維持したい場合はコメントアウトを解除
//             // DontDestroyOnLoad(gameObject);
//         }
//         else
//         {
//             Destroy(gameObject);
//         }
//     }

//     /// <summary>
//     /// Timeline（Director）が再生を開始した際に呼び出され、操作対象として登録します。
//     /// CutsceneHookスクリプトから自動的に呼び出されます。
//     /// </summary>
//     /// <param name="director">再生を開始したPlayableDirector</param>
//     public void RegisterDirector(PlayableDirector director)
//     {
//         activeDirector = director;

//         // Directorのグラフが有効なら、現在の設定速度をデフォルトとして保存します。
//         // （もし既に早送り状態だった場合に上書きしないよう、本来はチェックが必要ですが、
//         //   再生開始時は通常速度である前提で実装しています）
//         if (director.playableGraph.IsValid())
//         {
//             defaultSpeed = director.playableGraph.GetRootPlayable(0).GetSpeed();
//         }
//         else
//         {
//             defaultSpeed = 1.0;
//         }

//         // 新しいTimelineが始まったので早送りフラグはリセット
//         isFastForwarding = false;

//         // Debug.Log($"Timeline Registered: {director.name}");
//     }

//     /// <summary>
//     /// Timelineが停止した際に呼び出され、登録を解除します。
//     /// </summary>
//     /// <param name="director">停止したPlayableDirector</param>
//     public void UnregisterDirector(PlayableDirector director)
//     {
//         // 登録されているDirectorと一致する場合のみ解除処理を行います。
//         // （別のTimelineが同時並行で動いて上書きされた場合などを考慮）
//         if (activeDirector == director)
//         {
//             // Debug.Log($"Timeline Unregistered: {director.name}");
//             activeDirector = null;
//             isFastForwarding = false;

//             // 安全策：Timeline終了時にSEミュートが残らないように解除
//             if (SEManager.instance != null)
//                 SEManager.instance.IsTimelineMuted = false;
//         }
//     }

//     /// <summary>
//     /// 外部入力（UIボタンやキー入力）から呼び出し、早送りモードを切り替えます。
//     /// </summary>
//     /// <param name="active">true:早送り開始, false:通常速度に戻す</param>
//     public void SetFastForward(bool active)
//     {
//         // アクティブなDirectorがない、またはグラフが無効なら何もしない
//         if (activeDirector == null || !activeDirector.playableGraph.IsValid())
//             return;

//         if (active)
//         {
//             // 早送り開始処理
//             if (!isFastForwarding)
//             {
//                 isFastForwarding = true;

//                 // 早送り中のSE重複再生（マシンガン音）を防ぐためミュート
//                 if (SEManager.instance != null)
//                     SEManager.instance.IsTimelineMuted = true;

//                 // 速度を変更
//                 activeDirector.playableGraph.GetRootPlayable(0).SetSpeed(fastForwardSpeed);
//             }
//         }
//         else
//         {
//             // 通常速度への復帰処理
//             if (isFastForwarding)
//             {
//                 isFastForwarding = false;

//                 // ミュート解除
//                 if (SEManager.instance != null)
//                     SEManager.instance.IsTimelineMuted = false;

//                 // 速度を元に戻す
//                 activeDirector.playableGraph.GetRootPlayable(0).SetSpeed(defaultSpeed);
//             }
//         }
//     }

//     /// <summary>
//     /// 外部入力から呼び出し、現在再生中のTimelineを即座に終了（全スキップ）させます。
//     /// </summary>
//     public void SkipActiveTimeline()
//     {
//         if (activeDirector == null || !activeDirector.playableGraph.IsValid())
//             return;

//         // 1. SE対策
//         // スキップ時の大量再生を防ぐためミュートし、現在鳴っている音も停止します。
//         if (SEManager.instance != null)
//         {
//             SEManager.instance.IsTimelineMuted = true;
//             SEManager.instance.StopAllSE();
//         }

//         // 2. 時間のジャンプ
//         // 完全に duration にするとクリップの判定外(Weight=0)になることがあるため、
//         // ほんの少しだけ手前(0.05秒前)にする。
//         activeDirector.time = System.Math.Max(0, activeDirector.duration - 0.05);

//         // 3. 状態の確定 (Evaluate)
//         // ここが重要：時間を飛ばしただけではオブジェクトの位置などが更新されないため、
//         // Evaluateを呼んで「最終フレームの状態」をシーンに反映させます。
//         // これにより、CameraAreaTrackやWarpTrackの結果が適用されます。
//         activeDirector.Evaluate();

//         // 4. Fade対策
//         // スキップ後、画面が暗転したままにならないよう、フェードが残っていたら強制的に透明にします。
//         if (FadeCanvas.instance != null && FadeCanvas.instance.CurrentAlpha > 0.01f)
//         {
//             FadeCanvas.instance.SetAlpha(0f);
//         }

//         // 5. 停止処理の分岐
//         // WrapModeが「Hold」の場合、Stop()するとカメラがプレイヤーに戻ってしまうため、
//         // StopではなくPause()で止めて、Hold状態を維持させます。
//         if (activeDirector.extrapolationMode == DirectorWrapMode.Hold)
//         {
//             activeDirector.Pause();
//             // ※Holdの場合、OnGraphStopは呼ばれません。
//             // 後で手動でStopTimelineコマンドを呼ぶまでカメラ位置は維持されます。
//             Debug.Log("[TimelineSkipManager] Skipped Timeline with Hold WrapMode, paused instead of stopped.");
//         }
//         else
//         {
//             activeDirector.Stop();
//             // WrapModeがNoneやLoopなら、完全に停止してリセットします。
//             Debug.Log("[TimelineSkipManager] Skipped Timeline and stopped.");
//         }

//         // 6. 復帰処理
//         // 次のフレーム以降で音が鳴るようにミュートを解除します。
//         if (SEManager.instance != null)
//         {
//             SEManager.instance.IsTimelineMuted = false;
//         }

//         // 状態リセット
//         isFastForwarding = false;

//         // Holdの場合はまだDirectorが生きて機能しているので、activeDirectorの参照は消さないでおく手もありますが、
//         // 「スキップ操作」としては完了したため、ここでは参照を切っておきます。
//         // （次にStopTimelineコマンドが呼ばれる際は、そのコマンドがDirectorを知っているため問題ありません）
//         activeDirector = null;
//     }

//     // デバッグ用: キー入力で動作確認を行う場合に使用します。
//     // 本番環境ではInputSystemやUIボタンイベントから呼び出してください。
//     private void Update()
//     {
//         // Zキー長押しで早送り
//         if (Input.GetKeyDown(KeyCode.Z))
//         {
//             SetFastForward(true);
//             // Debug.Log("Fast Forward Started");
//         }
//         if (Input.GetKeyUp(KeyCode.Z))
//         {
//             SetFastForward(false);
//             //Debug.Log("Fast Forward Stopped");
//         }

//         // Tキーで全スキップ
//         if (Input.GetKeyDown(KeyCode.T))
//         {
//             SkipActiveTimeline();
//             // Debug.Log("Timeline Skipped");
//         }
//     }
// }
