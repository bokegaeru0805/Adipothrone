using DG.Tweening;
using UnityEngine;

public class FastTravelPoint : MonoBehaviour
{
    [Header("ファストトラベルポイントのデータ")]
    [SerializeField]
    private FastTravelPointData fastTravelPointData;
    private bool isUnLocked = false;
    private Animator animator;
    private SpriteRenderer spriteRenderer;

    // 起動前：淡い青（透明感のある未起動状態）
    private Color inactiveColor = new Color(150f / 255f, 180f / 255f, 255f / 255f);

    // 起動後：白（使用可能）
    private Color activeColor = new Color(1f, 1f, 1f);
    private float floatingHeight = 1f; //上下に浮遊する移動幅
    private float floatingDuration = 2.0f; //浮遊アニメーションの片道にかかる時間（秒）
    private Vector3 initialPosition; // 浮遊アニメーションの基準となる初期座標
    private GameManager gameManager;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        initialPosition = transform.position; // 初期位置を保存

        if (fastTravelPointData == null)
        {
            Debug.LogError($"{this.name} の FastTravelPointData が設定されていません。");
            return;
        }
        else
        {
            Vector3 fastTravelPointPosition = fastTravelPointData.targetPosition;
            if (fastTravelPointPosition != null)
            {
                if (this.transform.position != fastTravelPointPosition)
                {
                    Debug.LogWarning(
                        $"{this.name} の位置が FastTravelPointData の targetPosition と一致しません。"
                    );
                }
            }
        }
    }

    private void Start()
    {
        // OnEnableでも呼ばれるが、Startでも念のため呼び出すことで、
        // 実行順の問題を回避し、確実に初期状態が設定されるようにする。
        UpdateUnlockState();

        gameManager = GameManager.instance;
    }

    private void OnEnable()
    {
        // セーブデータに基づいて表示状態を更新
        UpdateUnlockState();
    }

    /// <summary>
    /// 現在のセーブデータに基づいて、ファストトラベルポイントの表示状態（アクティブ/非アクティブ）を更新します。
    /// </summary>
    private void UpdateUnlockState()
    {
        if (!GameManager.isFirstGameSceneOpen)
        {
            //ゲームシーンがまだ開かれていない場合は何もしない
            return;
        }

        // --- 安全対策（ガード節） ---
        // GameManagerやセーブデータがまだ準備できていない場合は、エラーを防ぐために処理を中断する。
        // OnEnableはStartより先に呼ばれる可能性があるため、このチェックは非常に重要。
        var fastTravelData = GameManager.instance?.savedata?.FastTravelData;
        if (fastTravelData == null || fastTravelPointData == null)
        {
            // 準備ができていない場合は、デフォルトの非アクティブ状態にしておく
            SetInactiveState();
            return;
        }

        // --- 元のロジックをここに移動 ---
        if (
            fastTravelData.unlockedFastTravels != null
            && fastTravelData.unlockedFastTravels.Count > 0
        )
        {
            // このファストトラベルポイントが登録されているか確認
            isUnLocked = fastTravelData.IsFastTravelDataRegistered(
                fastTravelPointData.fastTravelId
            );

            if (isUnLocked)
            {
                SetActiveState(); //アクティブ状態にする
            }
            else
            {
                SetInactiveState(); //非アクティブ状態にする
            }
        }
        else
        {
            // まだ一つもファストトラベルポイントが解放されていない場合
            SetInactiveState();
        }
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        if (Time.timeScale > 0)
        {
            if (
                InputManager.instance.GetInteract()
                && collision.CompareTag(GameConstants.PLAYER_TAG_NAME)
                && !gameManager.IsTalking
            )
            {
                if (!isUnLocked)
                {
                    //ファストトラベルポイントが未登録の場合、登録する
                    GameManager.instance.savedata.FastTravelData.RegisterFastTravelData(
                        fastTravelPointData.fastTravelId
                    );
                    SetActiveState(); //アクティブ状態にする
                    isUnLocked = true;
                }
                GameUIManager.instance.OpenFastTravelPanel();
            }
        }
    }

    private void SetInactiveState()
    {
        // このオブジェクトに紐づくDOTweenの動作をすべて停止
        transform.DOKill();
        // 座標をアニメーション開始前の初期位置に戻す
        transform.position = initialPosition;

        spriteRenderer.color = inactiveColor;
        animator.SetBool("IsCrystalActive", false); //アニメーションを停止
    }

    private void SetActiveState()
    {
        spriteRenderer.color = activeColor;
        animator.SetBool("IsCrystalActive", true); //アニメーションを開始

        // 既存のTweenがあれば停止してから新しいTweenを開始する（安全のため）
        transform.DOKill();

        // DOMoveYを使って、Y軸方向にアニメーションさせる
        transform
            .DOMoveY(initialPosition.y + floatingHeight, floatingDuration)
            .SetEase(Ease.InOutSine) // 動きの緩急をサインカーブのように滑らかにする
            .SetLoops(-1, LoopType.Yoyo) // 無限に（-1）、行って戻ってくる（Yoyo）ループを設定
            .SetUpdate(UpdateType.Normal); // Time.timeScaleの影響を受けるように設定（デフォルト）
    }

    /// <summary>
    /// このコンポーネントが無効になる、またはオブジェクトが破棄される際に呼び出されます。
    /// 確実にDOTweenアニメーションを停止させ、エラーを防ぎます。
    /// </summary>
    private void OnDisable()
    {
        // OnDestroyよりも早く、かつオブジェクトが無効になるだけでも呼ばれるOnDisableでKillするのが、より安全な停止方法です。
        transform.DOKill();
    }
}
