using System.Collections;
using UnityEngine;

public class CrystalSwitch : MonoBehaviour
{
    private FlagManager flagManager;

    [SerializeField]
    private KeyID button_number;

    [SerializeField]
    private ObjectName objectname;

    [SerializeField]
    private Sprite sprite2;

    [SerializeField]
    private GameObject ControlObject;
    private bool isPush; //スイッチが押されたかどうか　saveすべき変数
    private Sprite sprite1;
    private SpriteRenderer spriteRenderer;

    // 起動前：淡い赤（未使用のスイッチ）
    private Color SwitchInactiveColor = new Color(255f / 255f, 150f / 255f, 150f / 255f); //FF9999

    // 起動後：淡い緑（ON状態）
    private Color SwitchActiveColor = new Color(170f / 255f, 255f / 255f, 190f / 255f); //AAFFBE

    private enum ObjectName
    {
        none,
        DonutCrystal,
    }

    void Awake()
    {
        isPush = false;
        spriteRenderer = GetComponent<SpriteRenderer>();
        sprite1 = spriteRenderer.sprite; // 初期スプライトを保存
        spriteRenderer.color = SwitchInactiveColor; // 初期色を設定
        this.tag = GameConstants.InteractableObjectTagName; // 初期タグを設定
    }

    private void Start()
    {
        UpdateSwitchState(FlagManager.instance.GetKeyOpened(button_number));
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        if (Time.timeScale > 0)
        {
            if (
                !isPush
                && InputManager.instance.GetInteract()
                && collision.CompareTag(GameConstants.PlayerTagName)
            )
            {
                switch (objectname)
                {
                    case ObjectName.DonutCrystal:
                        if (
                            FlagManager.instance.GetIntFlag(PrologueCountedEvent.DonutMountainCount)
                            <= 3
                        )
                        {
                            return;
                        }
                        break;
                }

                isPush = true;
                this.tag = "Untagged";
                FlagManager.instance.SetKeyOpened(button_number, true);
                // 押した瞬間のレスポンスを良くするため、直接更新メソッドを呼んでも良い
                // UpdateSwitchState(true); // この行は無くてもイベントで動作するが、あった方が反応が速く感じる
                SEManager.instance?.PlayFieldSE(SE_Field.SwitchOn);
            }
        }
    }

    private void HandleKeyFlagChanged(KeyID changedKey, bool isOpened)
    {
        // 変更されたキーが、このスイッチに関係あるものか確認
        if (changedKey == button_number)
        {
            // 関係あるキーの状態が変わったので、スイッチの見た目も更新
            UpdateSwitchState(isOpened);
        }
    }

    private void UpdateSwitchState(bool isOpened)
    {
        isPush = isOpened;
        if (isOpened)
        {
            this.tag = "Untagged";
            spriteRenderer.sprite = sprite2;
            spriteRenderer.color = SwitchActiveColor;
            if (ControlObject != null)
            {
                ControlObject.SetActive(false);
            }
        }
        else
        {
            spriteRenderer.sprite = sprite1;
            spriteRenderer.color = SwitchInactiveColor;
            if (ControlObject != null)
            {
                ControlObject.SetActive(true);
            }
        }
    }

    private void OnEnable()
    {
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
        // フラグが変更されたら、HandleKeyFlagChangedメソッドが呼ばれるように登録
        FlagManager.OnKeyFlagChanged += HandleKeyFlagChanged;
    }

    private void OnDisable()
    {
        // オブジェクトが無効になる際に、登録を解除（メモリリーク防止）
        FlagManager.OnKeyFlagChanged -= HandleKeyFlagChanged;
    }
}
