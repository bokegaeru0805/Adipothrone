using System.Collections;
using Cinemachine;
using DG.Tweening;
using Effekseer;
using UnityEngine;
using UnityEngine.SceneManagement;

public class EventManager_Second : MonoBehaviour
{
    [Header("イベント用オブジェクト参照")]
    [SerializeField, Tooltip("イベント演出で使用するプレイヤーのオブジェクト")]
    private GameObject eventPlayer;

    [SerializeField, Tooltip("イベント演出で使用するロボットのオブジェクト")]
    private GameObject eventRobot;

    [SerializeField, Tooltip("操作可能な本物のプレイヤーオブジェクト")]
    private GameObject realPlayerObject;

    [Header("移動座標・オフセット")]
    [SerializeField]
    private Vector2 playerFirstPosition;

    [SerializeField]
    private Vector2 robotFirstPosition = new Vector2(-91, 4);

    [SerializeField]
    private Vector2 robotSecondPosition = new Vector2(-81, 3);

    [SerializeField]
    private float cameraFirstPosition_x = -91.0f;

    [SerializeField]
    private float cameraSecondPosition_x = -66.0f;

    [Header("エフェクト設定")]
    [SerializeField]
    private EffekseerEffectAsset warpEffectBefore;

    [SerializeField]
    private EffekseerEffectAsset warpEffectAfter;

    [SerializeField]
    private float warpEffectOffsetY = 0.0f;
    private Animator playerAnimator;
    private SpriteRenderer playerRenderer;
    private SpriteRenderer robotRenderer;

    // --- 状態管理フラグ ---
    private bool isTeleporting = false;
    private const string EVENT_TWEEN_ID = "SecondEventTween"; // このイベントのTweenを識別するためのID

    private void Awake()
    {
        if (eventPlayer != null)
        {
            playerAnimator = eventPlayer.GetComponent<Animator>();
            playerRenderer = eventPlayer.GetComponent<SpriteRenderer>();
        }
        else
            Debug.LogError("eventPlayerが設定されていません。");

        if (eventRobot != null)
        {
            robotRenderer = eventRobot.GetComponent<SpriteRenderer>();
        }
        else
            Debug.LogError("eventRobotが設定されていません。");

        if (realPlayerObject == null)
        {
            Debug.LogError("realPlayerObjectが設定されていません。");
        }
    }

    private void Start()
    {
        bool isSecondPrologueEndStart = FlagManager.instance.GetBoolFlag(
            PrologueTriggeredEvent.SecondPrologueEndStart
        );
        if (isSecondPrologueEndStart)
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Update()
    {
        // DOTweenのIDを指定して、関連するTweenだけをスキップする
        if (InputManager.instance.SkipDialogHold())
        {
            // テレポート中の重要なアニメーションはスキップしない
            if (!isTeleporting)
            {
                DOTween.Complete(EVENT_TWEEN_ID);
            }
        }
    }

    /// <summary>
    /// このオブジェクトが破棄される時に自動的に呼ばれるメソッド
    /// </summary>
    private void OnDestroy()
    {
        // このイベントに関連するすべてのTweenを安全に停止・破棄する
        // falseを指定すると、完了時のコールバックなどを呼ばずに即座にキルする
        DOTween.Kill(EVENT_TWEEN_ID);
    }

    public IEnumerator EventStart()
    {
        FadeCanvas.instance.FadeOut(0.5f);
        yield return new WaitForSecondsRealtime(0.5f);

        if (realPlayerObject != null)
            realPlayerObject.SetActive(false);

        eventPlayer.SetActive(true);
        eventPlayer.transform.position = new Vector3(
            playerFirstPosition.x,
            playerFirstPosition.y,
            eventPlayer.transform.position.z
        );
        playerAnimator.SetInteger("BodyState", GameConstants.AnimBodyState_Armed2);

        Camera.main.GetComponent<CinemachineBrain>().enabled = false;
        Camera.main.transform.position = new Vector3(
            cameraFirstPosition_x,
            playerFirstPosition.y + 6,
            Camera.main.transform.position.z
        );
        FadeCanvas.instance.FadeIn(0.5f);
    }

    public IEnumerator CameraToFlesh()
    {
        // SetIdの前に.SetLink(gameObject)を追加すると、より安全になります
        yield return Camera
            .main.transform.DOLocalMoveX(cameraSecondPosition_x, 2.0f)
            .SetLink(gameObject) // このオブジェクトが破壊されたらTweenも自動でKillされる
            .SetId(EVENT_TWEEN_ID);
    }

    public void RobotAppear()
    {
        eventRobot.SetActive(true);
        eventRobot.transform.position = new Vector3(
            robotFirstPosition.x,
            robotFirstPosition.y,
            eventPlayer.transform.position.z
        );

        Vector2 targetPos = robotSecondPosition;

        // SetIdの前に.SetLink(gameObject)を追加すると、より安全になります
        eventRobot
            .transform.DOLocalMove(targetPos, 2f)
            .SetLink(gameObject) // このオブジェクトが破壊されたらTweenも自動でKillされる
            .SetId(EVENT_TWEEN_ID);
    }

    public void EffectStart()
    {
        SEManager.instance?.PlaySystemEventSE(SE_SystemEvent.WarpStandby1);
    }

    public void Teleport()
    {
        isTeleporting = true; // スキップ不能な区間に入る

        Vector3 warpEffectPosition = new Vector3(
            eventPlayer.transform.position.x,
            eventPlayer.transform.position.y + warpEffectOffsetY,
            eventPlayer.transform.position.z
        );

        if (warpEffectBefore != null)
        {
            EffekseerSystem.PlayEffect(warpEffectBefore, warpEffectPosition);
        }

        // キャッシュしたコンポーネントを使用
        eventPlayer.transform.DOScale(Vector2.zero, 0.50f);
        playerRenderer.material.DOFade(0, 0.50f);
        eventRobot.transform.DOScale(Vector2.zero, 0.50f);
        robotRenderer.material.DOFade(0, 0.50f);

        SEManager.instance?.StopSystemEventSE(SE_SystemEvent.Vanish1);
        SEManager.instance?.PlaySystemEventSE(SE_SystemEvent.Warp1);
    }

    public void ToChapter1()
    {
        // 元の処理はすべて新しいコルーチンに移動し、
        // ここではそのコルーチンを開始するだけにする
        StartCoroutine(LoadChapter1Async());
    }

    /// <summary>
    /// Chapter1のシーンを非同期でロードするコルーチン
    /// </summary>
    private IEnumerator LoadChapter1Async()
    {
        // 演出①：画面をフェードアウトさせる
        FadeCanvas.instance.FadeOut(1.0f);
        // フェードアウトが終わるまで1秒待つ
        yield return new WaitForSeconds(1.0f);

        // --- 元々ToChapter1にあった処理をここに移動 ---
        eventPlayer.SetActive(false);
        eventRobot.SetActive(false);
        SEManager.instance?.StopAllSE();

        PlayerManager.instance?.SetPlayerBoolStatus(PlayerStatusBoolName.isRobotmove, true);

        if (GameManager.instance?.savedata?.FastTravelData != null)
        {
            var ftData = GameManager.instance.savedata.FastTravelData;
            ftData.RemoveFastTravelData(FastTravelName.TutorialStage);
            ftData.RegisterFastTravelData(FastTravelName.FirstVillage);
            ftData.SetLastUsedFastTravel(FastTravelName.FirstVillage);
        }
        else
        {
            Debug.LogWarning("FastTravelDataが存在しません");
        }

        GameManager.instance.SetCrossSceneSpawnPoint(new Vector2(-200f, 0));
        GameManager.instance.EndTalk(); //念のため、会話の終了フラグを立てる
        TimeManager.instance.ReleasePause(); //念のため、時間を通常に戻す
        PlayerManager.instance.UnlockControl(); //念のため、プレイヤーの操作を再開する

        // -----------------------------------------

        // 演出②：非同期でシーンのロードを開始する
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(GameConstants.SceneName_Chapter1);
    }
}
