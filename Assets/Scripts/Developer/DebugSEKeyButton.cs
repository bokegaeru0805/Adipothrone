using UnityEngine;
using UnityEngine.EventSystems; // UIイベントのインターフェースに必要
using UnityEngine.UI;

/// <summary>
/// ボタンにアタッチし、指定したキーボード入力でも動作するようにするスクリプト。
/// キーまたはマウスで押されている間、指定時間ごとに関数を呼び出し続ける。
/// </summary>
[RequireComponent(typeof(Button))]
[RequireComponent(typeof(CriWare.Assets.CriAtomSePlayer))]
public class DebugKeyButton
    : MonoBehaviour,
        IPointerDownHandler,
        IPointerUpHandler,
        IPointerExitHandler
{
    [Header("監視するキー")]
    [SerializeField, Tooltip("このボタンに対応するキーボードのキー")]
    private KeyCode targetKey = KeyCode.None;

    [Header("SE設定")]
    [SerializeField, Tooltip("再生するSEの名前")]
    private SE_UI seToPlay = SE_UI.Beep1;

    // --- 内部で管理する変数 ---
    private Button button;
    private CriWare.Assets.CriAtomSePlayer sePlayer; // SE再生用のCriAtomSePlayerコンポーネント
    private PointerEventData pointerEventData; // キー操作でUIイベントを偽装するため
    private bool isKeyPressed = false; // キーが押されているか
    private bool isMousePressed = false; // マウスで押されているか
    private float repeatTimer = 0f; // 長押し用のタイマー
    private float repeatDelay = 0.2f;  //長押し時に再度SEを鳴らすまでの時間（秒）

    private void Awake()
    {
        button = GetComponent<Button>();
        // イベントシステム（UI操作）の現在の情報を取得
        pointerEventData = new PointerEventData(EventSystem.current);
        sePlayer = GetComponent<CriWare.Assets.CriAtomSePlayer>();
    }

    private void Update()
    {
        // ボタンが操作不能な状態、またはキーが設定されていなければ何もしない
        if (button == null || !button.interactable || targetKey == KeyCode.None)
        {
            return;
        }

        // --- 1. キー入力の監視 ---

        // キーが押された瞬間
        if (Input.GetKeyDown(targetKey))
        {
            // まだ押されていなければ（マウスでもキーでも）
            if (!isKeyPressed && !isMousePressed)
            {
                // マウスでクリックされた時と同じイベント（OnPointerDown）を強制的に実行する
                // これにより、ボタンの見た目が「Pressed」状態に変わる
                ExecuteEvents.Execute(
                    button.gameObject,
                    pointerEventData,
                    ExecuteEvents.pointerDownHandler
                );

                // OnPointerDownが呼ばれるとisMousePressedフラグが立つので、
                // SE呼び出しとタイマーリセットはOnPointerDown側に任せる
            }
            isKeyPressed = true;
        }

        // キーが離された瞬間
        if (Input.GetKeyUp(targetKey))
        {
            isKeyPressed = false;

            // マウスでも押されていなければ（＝キーだけで押されていた場合）
            if (isMousePressed && !Input.GetMouseButton(0)) // マウスの左クリックも確認
            {
                // マウスを離した時と同じイベント（OnPointerUp）を強制的に実行する
                // これにより、ボタンの見た目が通常状態に戻る
                ExecuteEvents.Execute(
                    button.gameObject,
                    pointerEventData,
                    ExecuteEvents.pointerUpHandler
                );
            }
        }

        // --- 2. 長押し処理 ---

        // キーまたはマウスのどちらかで押されている間
        if (isKeyPressed || isMousePressed)
        {
            repeatTimer -= Time.deltaTime;
            if (repeatTimer <= 0f)
            {
                // SE関数を呼び出す
                CallSEFunction();
                // タイマーをリセット
                repeatTimer = repeatDelay;
            }
        }
    }

    // --- 3. マウス入力の監視 (インターフェースの実装) ---

    // マウスでクリックされた瞬間に呼ばれる
    public void OnPointerDown(PointerEventData eventData)
    {
        if (!button.interactable)
            return;

        isMousePressed = true;

        // SE関数を呼び出す（最初の1回）
        CallSEFunction();
        // 長押しタイマーをリセット
        repeatTimer = repeatDelay;
    }

    // マウスを離した瞬間に呼ばれる
    public void OnPointerUp(PointerEventData eventData)
    {
        if (!button.interactable)
            return;

        // キーも押されていなければ、完全に「離された」状態にする
        if (!isKeyPressed)
        {
            isMousePressed = false;
        }
        // (キーがまだ押されている場合は、isMousePressedはtrueのままになり、Pressed状態が維持される)
    }

    // マウスがボタンの領域から出た場合に呼ばれる
    public void OnPointerExit(PointerEventData eventData)
    {
        if (!button.interactable)
            return;

        // キーも押されていなければ、状態をリセット
        if (!isKeyPressed)
        {
            isMousePressed = false;
        }
    }

    // --- 4. SE関数 ---

    /// <summary>
    /// SEを鳴らすための関数
    /// </summary>
    private void CallSEFunction()
    {
        // SEを再生
        sePlayer.Play(seToPlay);
    }

    // スクリプトが無効になったら、状態をリセット
    private void OnDisable()
    {
        // 押されている途中で無効になった場合、ボタンの状態をリセットする必要がある
        bool wasPressed = isKeyPressed || isMousePressed;

        isKeyPressed = false;
        isMousePressed = false;
        repeatTimer = 0f;

        // 押されている状態だった場合、ボタンの見た目を元に戻す
        if (button != null && wasPressed)
        {
            // OnPointerUpイベントを強制的に実行し、ボタンの見た目を通常状態に戻す
            ExecuteEvents.Execute(
                button.gameObject,
                pointerEventData,
                ExecuteEvents.pointerUpHandler
            );
        }
    }
}
