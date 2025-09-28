using UnityEngine;

public class InteractableObject_Prologue : MonoBehaviour
{
    public Fungus.Flowchart flowchart = null;

    [SerializeField]
    private Sprite sprite2;

    [SerializeField]
    private Sprite sprite3; // sprite3は現在使われていませんが、フィールドは残しておきます

    [SerializeField, Tooltip("自分のオブジェクトの名前を選択してください")]
    private ObjectName objectname;

    // [SerializeField]
    // private GameObject ControlObject = null;

    private Sprite sprite1;
    private SpriteRenderer spriteRenderer;

    private enum ObjectName
    {
        donutMountain,
    }

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        sprite1 = spriteRenderer.sprite; // 初期スプライトを保存
    }

    private void Start()
    {
        //起動時に一度だけ、現在のフラグ値に基づいて状態を更新
        if (FlagManager.instance != null)
        {
            UpdateStateByCount(
                FlagManager.instance.GetIntFlag(PrologueCountedEvent.DonutMountainCount)
            );
        }
        else
        {
            Debug.LogError("FlagManagerが見つかりません。");
        }
    }

    // イベントの購読・解除
    private void OnEnable()
    {
        FlagManager.OnIntFlagChanged += HandleIntFlagChanged;
    }

    private void OnDisable()
    {
        FlagManager.OnIntFlagChanged -= HandleIntFlagChanged;
    }

    /// <summary>
    /// Int型フラグの変更イベントを受け取って処理するメソッド
    /// </summary>
    private void HandleIntFlagChanged(System.Enum flag, int newCount)
    {
        // 変更されたフラグがDonutMountainCountであるか確認
        if (
            flag is PrologueCountedEvent countedEvent
            && countedEvent == PrologueCountedEvent.DonutMountainCount
        )
        {
            UpdateStateByCount(newCount);
        }
    }

    /// <summary>
    /// カウント数に応じて、オブジェクトの状態（見た目やタグ）を更新する専用メソッド
    /// </summary>
    private void UpdateStateByCount(int count)
    {
        // スプライトを更新
        // カウントが3以下ならsprite1、3より大きいならsprite2に
        spriteRenderer.sprite = count <= 3 ? sprite1 : sprite2;
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        if (
            Time.timeScale > 0
            && !GameManager.IsTalking
            && InputManager.instance.GetInteract()
            && collision.CompareTag(GameConstants.PlayerTagName)
        )
        {
            // このオブジェクトが操作可能な場合のみ処理
            if (tag == GameConstants.InteractableObjectTagName)
            {
                switch (objectname)
                {
                    case ObjectName.donutMountain:
                        if (flowchart != null)
                        {
                            FungusHelper.ExecuteBlock(flowchart, "DonutMountainField");
                            // フラグを増やすだけ。見た目の更新はイベント経由で自動的に行われる
                            FlagManager.instance.IncrementIntFlag(
                                PrologueCountedEvent.DonutMountainCount,
                                1
                            );
                        }
                        break;
                }
            }
        }
    }
}