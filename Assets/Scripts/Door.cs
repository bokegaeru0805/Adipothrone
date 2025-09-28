using UnityEngine;

public class Door : MonoBehaviour
{
    [SerializeField]
    private int door_number;
    private bool isDoorOpen = false;
    private bool isFirstCheck = true; // 初回チェックフラグ
    private BoxCollider2D boxCollider;
    private SpriteRenderer spriteRenderer;

    private void Awake()
    {
        boxCollider = GetComponent<BoxCollider2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Start()
    {
        CheckDoorState();
    }

    private void OnEnable()
    {
        // いずれかのキーの状態が変更されたら、CheckDoorStateメソッドを呼び出すように登録
        FlagManager.OnKeyFlagChanged += HandleKeyFlagChanged;
    }

    private void OnDisable()
    {
        // オブジェクトが無効になる際に登録を解除し、メモリリークを防ぐ
        FlagManager.OnKeyFlagChanged -= HandleKeyFlagChanged;
    }

    /// <summary>
    /// キーの状態変化イベントを受け取って、ドアの状態を再チェックする
    /// </summary>
    private void HandleKeyFlagChanged(KeyID keyId, bool isOpened)
    {
        CheckDoorState();
    }

    /// <summary>
    /// ドアの開錠条件をチェックし、状態が変化していれば反映させるメソッド
    /// </summary>
    private void CheckDoorState()
    {
        // FlagManagerに問い合わせて、ドアが開くべきか確認
        bool shouldBeOpen = FlagManager.instance.IsDoorUnlocked(door_number);

        // 現在の状態と変わらないなら、何もしない
        if (shouldBeOpen == isDoorOpen)
        {
            return;
        }

        // 状態が変化したので、更新する
        isDoorOpen = shouldBeOpen;

        //状態に応じた処理
        boxCollider.enabled = !isDoorOpen; // 衝突判定を更新
        spriteRenderer.enabled = !isDoorOpen; // 表示状態を更新

        if (isFirstCheck)
        {
            isFirstCheck = false; // 初回チェック後はフラグを更新
            return; // 初回チェックではSEを鳴らさない
        }
        
        // SEを鳴らす
        if (isDoorOpen)
        {
            SEManager.instance?.PlayFieldSE(SE_Field.DoorOpen_Metal);
        }
        else
        {
            SEManager.instance?.PlayFieldSE(SE_Field.DoorOpenLock);
        }
    }
}
