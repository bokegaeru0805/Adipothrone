using DG.Tweening;
using NaughtyAttributes;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class FastTravelPoint : MonoBehaviour
{
    #region Inspector設定

    [Header("ファストトラベルポイントのデータ")]
    [SerializeField]
    private FastTravelPointData fastTravelPointData;

    /// <summary>
    /// Inspectorで確認するため、参照中のDataが持つ移動先座標を読み取り専用で表示します。
    /// </summary>
    [ShowNativeProperty]
    private string DataTargetPosition =>
        fastTravelPointData != null
            ? fastTravelPointData.targetPosition.ToString("F1")
            : "FastTravelPointData 未設定";

    #endregion

    #region 表示設定

    // 未解放時は淡い青、解放後は白で表示する。
    private Color inactiveColor = new Color(150f / 255f, 180f / 255f, 255f / 255f);
    private Color activeColor = new Color(1f, 1f, 1f);
    private float floatingHeight = 1f;
    private float floatingDuration = 2.0f;

    #endregion

    #region 内部状態

    private bool isUnLocked = false;
    private Animator animator;
    private SpriteRenderer spriteRenderer;
    private Vector3 initialPosition;
    private GameManager gameManager;

    #endregion

    #region Unityライフサイクル

    private void Awake()
    {
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        initialPosition = transform.position;

        if (fastTravelPointData == null)
        {
            Debug.LogError($"{name} の FastTravelPointData が設定されていません。", this);
            return;
        }

        if ((Vector2)transform.position != fastTravelPointData.targetPosition)
        {
            Debug.LogWarning(
                $"{name} の位置が FastTravelPointData の targetPosition と一致しません。",
                this
            );
        }
    }

    private void OnEnable()
    {
        UpdateUnlockState();
    }

    private void Start()
    {
        gameManager = GameManager.instance;

        // OnEnable時点でManagerの準備が完了していない場合に備えて再同期する。
        UpdateUnlockState();
    }

    private void OnDisable()
    {
        // 無効化後もオブジェクトに紐づくTweenが残らないよう停止する。
        transform.DOKill();
    }

    #endregion

    #region プレイヤー操作

    private void OnTriggerStay2D(Collider2D collision)
    {
        if (Time.timeScale <= 0)
        {
            return;
        }

        if (
            !InputManager.instance.GetInteract()
            || !collision.CompareTag(GameConstants.PLAYER_TAG_NAME)
            || gameManager.IsTalking
        )
        {
            return;
        }

        if (!isUnLocked)
        {
            GameManager.instance.savedata.FastTravelData.RegisterFastTravelData(
                fastTravelPointData.fastTravelId
            );
            SetActiveState();
            isUnLocked = true;
        }

        GameUIManager.instance.OpenFastTravelPanel();
    }

    #endregion

    #region 解放状態と表示

    /// <summary>
    /// 現在のセーブデータを参照し、解放状態と見た目を同期します。
    /// </summary>
    private void UpdateUnlockState()
    {
        if (!GameManager.isFirstGameSceneOpen)
        {
            return;
        }

        // OnEnableは各Managerの初期化より先に呼ばれる可能性がある。
        var fastTravelData = GameManager.instance?.savedata?.FastTravelData;
        if (fastTravelData == null || fastTravelPointData == null)
        {
            SetInactiveState();
            return;
        }

        isUnLocked =
            fastTravelData.unlockedFastTravels != null
            && fastTravelData.unlockedFastTravels.Count > 0
            && fastTravelData.IsFastTravelDataRegistered(fastTravelPointData.fastTravelId);

        if (isUnLocked)
        {
            SetActiveState();
        }
        else
        {
            SetInactiveState();
        }
    }

    /// <summary>
    /// 未解放時の色へ戻し、浮遊アニメーションを停止します。
    /// </summary>
    private void SetInactiveState()
    {
        transform.DOKill();
        transform.position = initialPosition;

        spriteRenderer.color = inactiveColor;
        animator.SetBool("IsCrystalActive", false);
    }

    /// <summary>
    /// 解放後の色とアニメーションを適用し、上下の浮遊を開始します。
    /// </summary>
    private void SetActiveState()
    {
        spriteRenderer.color = activeColor;
        animator.SetBool("IsCrystalActive", true);

        // 状態の再同期が複数回行われてもTweenが重複しないよう、既存Tweenを先に停止する。
        transform.DOKill();
        transform
            .DOMoveY(initialPosition.y + floatingHeight, floatingDuration)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo)
            .SetUpdate(UpdateType.Normal);
    }

    #endregion

    #region エディター補助

    private bool HasFastTravelPointData => fastTravelPointData != null;

    /// <summary>
    /// このオブジェクトの現在のXY座標を、参照中の移動先データへ保存します。
    /// </summary>
    [Button("現在位置を移動先に設定")]
    [ShowIf(nameof(HasFastTravelPointData))]
    private void SetCurrentPositionToTargetPosition()
    {
        if (fastTravelPointData == null)
        {
            return;
        }

        Vector2 previousPosition = fastTravelPointData.targetPosition;
        Vector2 currentPosition = transform.position;

#if UNITY_EDITOR
        // ScriptableObjectへの変更をUndo対象にし、アセットの保存対象としてマークする。
        Undo.RecordObject(fastTravelPointData, "Set Fast Travel Target Position");
#endif

        fastTravelPointData.targetPosition = currentPosition;

#if UNITY_EDITOR
        EditorUtility.SetDirty(fastTravelPointData);
#endif

        Debug.Log(
            $"{fastTravelPointData.name} の 設定座標を {currentPosition} に更新しました。",
            fastTravelPointData
        );
    }

    /// <summary>
    /// 選択中のシーンビューに、現在位置から設定済みの移動先までの線と目印を表示します。
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (fastTravelPointData == null)
        {
            return;
        }

        // targetPositionはVector2のため、シーン上で見やすいよう本体と同じZ平面に描画する。
        Vector3 targetPosition = new Vector3(
            fastTravelPointData.targetPosition.x,
            fastTravelPointData.targetPosition.y,
            transform.position.z
        );

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, targetPosition);
        Gizmos.DrawWireSphere(targetPosition, 0.5f);
    }

    #endregion
}
