using System.Collections;
using Fungus;
using UnityEngine;

public class FrontDoor_Event : MonoBehaviour
{
    [SerializeField]
    private Flowchart flowchart = null;

    [SerializeField]
    private Vector2 movepos = Vector2.zero; //移動位置を保存する変数

    [SerializeField]
    private DoorName doorname; //ドアの名前

    private enum DoorName
    {
        None = 0,
        Tutorial = 1,
        Village_Well = 4,
        Village_GirlHouse = 7,
    }

    private DoorTagState currentTagState = DoorTagState.None;

    // 扉の状態を明確に定義するenum
    private enum DoorTagState
    {
        None,
        AreaTransition, // 開けられる状態
        Interactable, // 調べられるだけの状態
        Untagged // 何も反応しない状態
        ,
    }

    private FlagManager flagManager = null;
    private bool isTalking = false; // 会話状態を保存するローカル変数

    private void Awake()
    {
        if (flowchart == null)
        {
            Debug.LogError("FrontDoor_EventはFlowchartを持っていません");
        }

        if (doorname == DoorName.None)
        {
            Debug.LogError($"{this.name}のdoornameが設定されていません");
        }

        if (movepos == Vector2.zero)
        {
            Debug.LogWarning($"{this.name}のmoveposが設定されていません");
        }
    }

    private void OnEnable()
    {
        // 他のコンポーネントの初期化を待ってから処理を開始する
        StartCoroutine(DelayedInitialization());
    }

    /// <summary>
    /// 全てのAwake/Startが完了するのを待ってから、初期化処理を実行するコルーチン
    /// </summary>
    private IEnumerator DelayedInitialization()
    {
        // 最初のフレームの描画が終わるまで待つ
        // これにより、全てのシングルトンが確実に初期化されている状態になる
        yield return new WaitForEndOfFrame();

        if (flagManager == null)
        {
            flagManager = FlagManager.instance;
            if (flagManager == null)
            {
                Debug.LogError(
                    "FlagManagerが見つかりません。FrontDoor_Eventが正しく動作しません。"
                );
                yield break;
            }
        }

        // FlagManagerのboolフラグ変更イベントに、自分のUpdateDoorTagメソッドを登録
        FlagManager.OnBoolFlagChanged += HandleFlagChange;

        GameManager.OnTalkingStateChanged += HandleTalkingStateChanged;


        // パネルが有効になった際に、一度現在の状態でタグを更新する
        UpdateDoorTag();
    }

    private void OnDisable()
    {
        if (!GameManager.isFirstGameSceneOpen)
            return;

        // オブジェクトが無効になる際に、イベントの登録を解除（メモリリーク防止）
        FlagManager.OnBoolFlagChanged -= HandleFlagChange;
        GameManager.OnTalkingStateChanged -= HandleTalkingStateChanged;

    }

    /// <summary>
    /// FlagManagerからイベントを受け取ったときに呼ばれるメソッド
    /// </summary>
    private void HandleFlagChange(System.Enum _flag, bool _value)
    {
        // どのフラグが変更されたかに関わらず、自身の状態を再評価してタグを更新する
        UpdateDoorTag();
    }

    /// <summary>
    /// 現在のフラグ状況に応じて、この扉がどの状態であるべきかを判定します。
    /// </summary>
    /// <returns>扉が取るべき状態（DoorTagState）</returns>
    private DoorTagState GetCurrentDoorState()
    {
        if (flagManager == null)
        {
            Debug.LogError("FlagManagerが未設定です。FrontDoor_Eventが正しく動作しません。");
            return DoorTagState.Untagged;
        }

        // --- 優先度1: 何も反応しない(Untagged)状態の判定 ---
        switch (doorname)
        {
            case DoorName.Village_Well:
                // 村の井戸は、クエスト未受注 または 討伐完了の場合は反応しない
                if (
                    !flagManager.GetBoolFlag(Chapter1TriggeredEvent.WellQuestReceived)
                    || flagManager.GetBoolFlag(Chapter1TriggeredEvent.WellEnemyDefeated)
                )
                {
                    return DoorTagState.Untagged;
                }
                break;
            case DoorName.Village_GirlHouse:
                // 少女の家は、行方不明フラグが立っていない場合は反応しない
                if (!flagManager.GetBoolFlag(Chapter1TriggeredEvent.ShopGirlMissing))
                {
                    return DoorTagState.Untagged;
                }
                break;
        }

        // --- 優先度2: 開けられる(AreaTransition)状態の判定 ---
        if (IsDoorOpenable())
        {
            return DoorTagState.AreaTransition;
        }

        // --- 優先度3: 上記以外は調べられる(Interactable)状態 ---
        return DoorTagState.Interactable;
    }

    /// <summary>
    /// 現在のゲーム進行状況に応じて、この扉が開ける状態にあるかを判定します。
    /// </summary>
    /// <returns>扉が開けるならtrue、そうでないならfalse。</returns>
    private bool IsDoorOpenable()
    {
        if (flagManager == null)
            return false;

        switch (doorname)
        {
            case DoorName.Tutorial:
                // チュートリアルの扉は、特定のボディステート以上であるか、一度開けたフラグが立っていれば開ける
                return PlayerBodyManager.instance.BodyState >= GameConstants.BodyState_Armed2
                    || flagManager.GetBoolFlag(PrologueTriggeredEvent.TutorialEventDoorOpened);

            case DoorName.Village_Well:
                // 村の井戸は、クエスト受注済み かつ 討伐未完了の場合のみ開ける
                return flagManager.GetBoolFlag(Chapter1TriggeredEvent.WellQuestReceived)
                    && !flagManager.GetBoolFlag(Chapter1TriggeredEvent.WellEnemyDefeated);

            case DoorName.Village_GirlHouse:
                // 少女の家は、行方不明フラグが立っている場合のみ開ける
                return flagManager.GetBoolFlag(Chapter1TriggeredEvent.ShopGirlMissing);

            default:
                return false;
        }
    }

    /// <summary>
    /// 扉の状態をチェックし、必要であればタグを更新します。
    /// </summary>
    private void UpdateDoorTag()
    {
        // 現在のフラグに基づき、扉がどうあるべき状態かを判断
        DoorTagState newState = GetCurrentDoorState();

        // 状態が前回から変化していない場合は、何もしない（最適化）
        if (currentTagState == newState)
        {
            return;
        }

        // 新しい状態をキャッシュ
        currentTagState = newState;

        // 状態に応じてタグを設定
        switch (newState)
        {
            case DoorTagState.AreaTransition:
                this.gameObject.tag = GameConstants.AreaTransitionTagName;
                break;
            case DoorTagState.Interactable:
                this.gameObject.tag = GameConstants.InteractableObjectTagName;
                break;
            case DoorTagState.Untagged:
                this.gameObject.tag = "Untagged";
                break;
        }
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        if (Time.timeScale > 0 && isTalking == false)
        { // ゲームが一時停止していない、かつトーク中でない場合
            if (
                InputManager.instance.GetInteract()
                && collision.gameObject.CompareTag(GameConstants.PlayerTagName)
            )
            {
                switch (doorname)
                {
                    case DoorName.Tutorial:
                        HandleTutorialDoorEvent();
                        break;
                    case DoorName.Village_Well:
                        HandleVillageWellDoorEvent();
                        break;
                    case DoorName.Village_GirlHouse:
                        HandleVillageGirlHouseDoorEvent();
                        break;
                    default:
                        // 未定義のドア名が設定されている場合の処理
                        Debug.LogWarning($"未定義のドア名: {doorname} が設定されています。", this);
                        break;
                }
            }
        }
    }

    // --- 各ドアごとの個別イベント処理 ---
    private void HandleTutorialDoorEvent()
    {
        if (PlayerBodyManager.instance.BodyState < GameConstants.BodyState_Armed2)
        {
            flowchart.SetBooleanVariable("isTutorialEventDoorOpen", false);
            FungusHelper.ExecuteBlock(flowchart, "TutorialEventDoor"); // Fungusのブロックを実行
        }
        else
        {
            if (!flagManager.GetBoolFlag(PrologueTriggeredEvent.TutorialEventDoorOpened))
            {
                flagManager.SetBoolFlag(PrologueTriggeredEvent.TutorialEventDoorOpened, true);
                flowchart.SetBooleanVariable("isTutorialEventDoorOpen", true);
                FungusHelper.ExecuteBlock(flowchart, "TutorialEventDoor"); // Fungusのブロックを実行
            }
            else
            {
                //プレイヤーが操作不能状態でない場合のみドアを開く
                if (!PlayerManager.instance.isControlLocked)
                {
                    DoorOpener.OpenDoor(movepos, this, DoorOpener.DoorType.MetalDoor);
                }
            }
        }
    }

    private void HandleVillageWellDoorEvent()
    {
        if (IsDoorOpenable())
        {
            //プレイヤーが操作不能状態でない場合のみドアを開く
            if (!PlayerManager.instance.isControlLocked)
            {
                DoorOpener.OpenDoor(movepos, this, DoorOpener.DoorType.Well);
            }
        }
    }

    private void HandleVillageGirlHouseDoorEvent()
    {
        if (IsDoorOpenable())
        {
            //プレイヤーが操作不能状態でない場合のみドアを開く
            if (!PlayerManager.instance.isControlLocked)
            {
                DoorOpener.OpenDoor(movepos, this, DoorOpener.DoorType.WoodenDoor);
            }
        }
    }

    /// <summary>
    /// GameManagerから会話状態の変更通知を受け取る
    /// </summary>
    private void HandleTalkingStateChanged(bool talkState)
    {
        isTalking = talkState;
    }

    // Gizmosを描画するためのメソッド
    private void OnDrawGizmos()
    {
        // ギズモの色を設定
        Gizmos.color = Color.green; // 緑色にする

        // Y方向のオフセット値を取得
        float yOffset = 0f;
        // オブジェクトにアタッチされているCollider2Dを取得
        Collider2D col = GetComponent<Collider2D>();

        // Collider2Dが存在する場合
        if (col != null)
        {
            // collider.bounds.extents.y は、コライダーの高さのちょうど半分
            yOffset = col.bounds.extents.y;
        }

        // このオブジェクトのワールド座標を取得
        Vector3 startPosition = transform.position;
        // Y座標にオフセットを加える
        startPosition.y += yOffset;

        // movePosはVector2なので、Z座標を0としてVector3に変換し、オフセットを加える
        Vector3 endPosition = new Vector3(movepos.x, movepos.y, startPosition.z);

        // オブジェクトの座標からmovePosまで線を引く
        Gizmos.DrawLine(startPosition, endPosition);
    }
}
