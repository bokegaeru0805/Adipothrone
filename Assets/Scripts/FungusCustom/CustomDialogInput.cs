using Fungus;
using UnityEngine;

// 元のDialogInputを「継承」して、その機能をすべて引き継ぐ
public class CustomDialogInput : DialogInput
{
    private InputManager inputManager;

    // 会話開始時にスキップキーが押されっぱなしの場合、キーが離されるまでスキップを無効にするためのフラグ
    private bool isWaitingForSkipKeyRelease = false;

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
        if (inputManager != null && inputManager.SkipDialogHold())
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
            }
            // キーが離されるまでは、以降の入力処理を一切行わない
            return;
        }

        // ここからが、追加したいカスタム処理
        // writer と InputManager の両方が存在することを確認
        if (writer != null && inputManager != null)
        {
            // InputManagerのUIConfirm (決定) または UICloseの長押し (キャンセル/早送り) を検知
            if (inputManager.UIConfirm() || (cancelEnabled && inputManager.SkipDialogHold()))
            {
                // 元のDialogInputが持っているSetNextLineFlag()を呼び出す
                SetNextLineFlag();
            }
        }
    }
}
