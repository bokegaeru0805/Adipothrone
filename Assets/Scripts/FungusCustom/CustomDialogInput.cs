using Fungus;
using UnityEngine;

// 元のDialogInputを「継承」して、その機能をすべて引き継ぐ
public class CustomDialogInput : DialogInput
{
    [Tooltip("スキップを開始するために必要な長押し時間（秒）")]
    [SerializeField]
    private float skipHoldThreshold = 0.3f;
    private InputManager inputManager;

    // 会話開始時にスキップキーが押されっぱなしの場合、キーが離されるまでスキップを無効にするためのフラグ
    private bool isWaitingForSkipKeyRelease = false;

    // スキップキーの押下時間を計測するためのタイマー
    private float currentSkipHoldTimer = 0.0f;

    private void Start()
    {
        // InputManagerのインスタンスを取得
        inputManager = InputManager.instance;
        if (inputManager == null)
        {
            Debug.LogError(
                "InputManagerが見つかりません。CustomDialogInputはInputManagerに依存しています。"
            );
        }
    }

    // このコンポーネントが有効になった時（Say Dialogが表示された時など）に呼び出される
    private void OnEnable()
    {
        // Start()よりも先にOnEnable()が呼ばれる可能性を考慮し、inputManagerをチェック・取得
        if (inputManager == null)
        {
            inputManager = InputManager.instance;
        }

        // inputManagerが取得できていれば、会話開始時のスキップキーの状態をチェック
        if (
            inputManager != null
            && inputManager.SkipDialogHold()
            && !(
                TimelineSkipManager.instance != null
                && TimelineSkipManager.instance.IsFastForwarding
            )
        )
        {
            // スキップキーが押されていたら、解放待ちフラグを立てる
            isWaitingForSkipKeyRelease = true;
        }
        else
        {
            // 押されていなければ、フラグは解除しておく
            isWaitingForSkipKeyRelease = false;
        }
    }

    // Updateメソッドを「上書き」して、独自の処理を追加する
    protected override void Update()
    {
        // まず、元のDialogInputが持っているUpdate処理をすべて実行する
        base.Update();

        // スキップキーの解放待ち状態の場合の処理
        if (isWaitingForSkipKeyRelease)
        {
            // スキップキーが離されたら、解放待ち状態を解除する
            if (!inputManager.SkipDialogHold())
            {
                isWaitingForSkipKeyRelease = false;
                currentSkipHoldTimer = 0f; // タイマーもリセット
            }
            // キーが離されるまでは、以降の入力処理を一切行わない
            return;
        }

        // ここからが、追加したいカスタム処理
        // writer と InputManager の両方が存在することを確認
        if (writer != null && inputManager != null)
        {
            // 変更部分: 決定ボタンとスキップ長押しを分けて処理

            // 1. UIConfirm (決定) は即座に反応させる
            if (inputManager.UIConfirm())
            {
                SetNextLineFlag();
                currentSkipHoldTimer = 0f; // 決定入力があったら長押しタイマーはリセット
            }
            // 2. スキップキーが押されている場合（かつキャンセル有効時）
            else if (cancelEnabled && inputManager.SkipDialogHold())
            {
                // 押されている時間を計測
                currentSkipHoldTimer += Time.deltaTime;

                // 閾値を超えたらスキップ処理（次の行へ進むフラグ）を実行し続ける
                if (currentSkipHoldTimer >= skipHoldThreshold)
                {
                    SetNextLineFlag();
                }
            }
            // 3. キーが離された場合
            else
            {
                // タイマーをリセット
                currentSkipHoldTimer = 0f;
            }
        }
    }
}
