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

    private void Awake()
    {
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();

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
        if (!GameManager.isFirstGameSceneOpen || fastTravelPointData == null)
        {
            //ゲームシーンがまだ開かれていない場合は何もしない
            // または、fastTravelPointDataが設定されていない場合も何もしない
            return;
        }

        var fastTravelData = GameManager.instance?.savedata?.FastTravelData;
        if (
            fastTravelData?.unlockedFastTravels != null
            && fastTravelData.unlockedFastTravels.Count > 0
        )
        {
            if (fastTravelPointData != null)
            {
                //このファストトラベルポイントが登録されているか確認
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
        }
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        if (Time.timeScale > 0)
        {
            if (
                InputManager.instance.GetInteract()
                && collision.CompareTag(GameConstants.PlayerTagName)
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
        spriteRenderer.color = inactiveColor;
        animator.SetBool("IsCrystalActive", false); //アニメーションを停止
    }

    private void SetActiveState()
    {
        spriteRenderer.color = activeColor;
        animator.SetBool("IsCrystalActive", true); //アニメーションを開始
    }
}
