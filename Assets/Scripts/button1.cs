using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class button1 : MonoBehaviour
{
    [SerializeField]
    private KeyID button_number;

    [SerializeField]
    private float button_speed;

    [SerializeField]
    private bool isStraightmove;

    [SerializeField]
    private bool isSwingmove;

    [SerializeField]
    private Vector2 button_startpos;

    [SerializeField]
    private Vector2 button_endpos;

    [SerializeField]
    private Sprite sprite; //ボタンが押された後のスプライト
    private bool isPush; //ボタンが押されたかどうか　saveすべき変数
    private float rangeX;
    private float rangeY;
    private float distance;
    private float vx = 0;
    private float vy = 0;
    private Rigidbody2D rbody;
    private Vector3 pos;
    private Sprite originalSprite;
    private SpriteRenderer spriteRenderer;

    private void Awake()
    {
        isPush = false;
        rbody = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        originalSprite = spriteRenderer.sprite;

        if (isStraightmove)
        {
            rangeX = button_endpos.x - button_startpos.x;
            rangeY = button_endpos.y - button_startpos.y;
            distance = (button_endpos - button_startpos).magnitude;
            this.transform.position = button_startpos;
            vx = button_speed * rangeX / distance;
            vy = button_speed * rangeY / distance;
        }
        else if (isSwingmove)
        {
            this.transform.position = button_startpos;
            vx = button_speed;
            vy = button_speed;
        }
    }

    private void Start()
    {
        UpdateState(FlagManager.instance.GetKeyOpened(button_number));
    }

    private void OnEnable()
    {
        FlagManager.OnKeyFlagChanged += HandleKeyFlagChanged;
    }

    private void OnDisable()
    {
        FlagManager.OnKeyFlagChanged -= HandleKeyFlagChanged;
    }

    private void HandleKeyFlagChanged(KeyID changedKey, bool isOpened)
    {
        if (changedKey == button_number)
        {
            UpdateState(isOpened);
        }
    }

    private void UpdateState(bool pushed)
    {
        isPush = pushed;
        spriteRenderer.sprite = pushed ? sprite : originalSprite;

        if (pushed)
        {
            isStraightmove = false;
            isSwingmove = false;
            if (rbody != null)
                rbody.velocity = Vector2.zero;
        }
        // 必要であれば、pushedがfalseになった際に移動を再開するロジックをここに追加
    }

    private void FixedUpdate()
    {
        if (isStraightmove)
        {
            rbody.velocity = new Vector2(vx, vy);
            var pos = transform.position;

            if (button_startpos.x != button_endpos.x)
            {
                if ((vx > 0 && pos.x > button_endpos.x) || (vx < 0 && pos.x < button_startpos.x))
                {
                    vx *= -1;
                    vy *= -1;
                }
            }
            else
            {
                if ((vy > 0 && pos.y > button_endpos.y) || (vy < 0 && pos.y < button_startpos.y))
                {
                    vx *= -1;
                    vy *= -1;
                }
            }
        }
        else if (isSwingmove)
        {
            rbody.velocity = new Vector2(vx, vy);
            var pos = transform.position;

            if (pos.x > button_endpos.x)
                vx = -button_speed;
            else if (pos.x < button_startpos.x)
                vx = button_speed;

            if (pos.y > button_endpos.y)
                vy = -button_speed;
            else if (pos.y < button_startpos.y)
                vy = button_speed;
        }
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (Time.timeScale > 0)
        {
            if (other.gameObject.tag == GameConstants.PlayerAttackTagName && !isPush)
            {
                // ▼▼▼【変更】直接FlagManagerにアクセスしてフラグを立てる ▼▼▼
                FlagManager.instance.SetKeyOpened(button_number, true);
                SEManager.instance?.PlayFieldSE(SE_Field.SwitchOn);
            }
        }
    }
}
