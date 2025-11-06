#if DEMO_BUILD
using UnityEngine;

/// <summary>
/// ゲームオブジェクトにアタッチし、指定したキーボード入力またはマウスの左クリックで動作するスクリプト。
/// キーまたはマウスで押されている間、指定時間ごとに関数を呼び出し続ける。
/// </summary>
[RequireComponent(typeof(CriWare.Assets.CriAtomSePlayer))]
public class DebugKeyInput : MonoBehaviour
{
    [Header("監視するキー")]
    [SerializeField, Tooltip("この処理に対応するキーボードのキー")]
    private KeyCode targetKey = KeyCode.None;

    [Header("SE設定")]
    [SerializeField, Tooltip("再生するSEの名前")]
    private SE_PlayerAction seToPlay = SE_PlayerAction.Damage1;

    // --- 内部で管理する変数 ---
    private CriWare.Assets.CriAtomSePlayer sePlayer; // SE再生用のコンポーネント
    private bool isKeyPressed = false; // キーが押されているか
    private bool isMousePressed = false; // マウスで押されているか
    private float repeatTimer = 0f; // 長押し用のタイマー
    private float repeatDelay = 0.2f;  //長押し時に再度SEを鳴らすまでの時間（秒）

    private void Awake()
    {
        sePlayer = GetComponent<CriWare.Assets.CriAtomSePlayer>();
    }

    private void Update()
    {
        // スクリプト自体が無効な状態なら何もしない
        if (!this.enabled)
        {
            return;
        }

        // --- 1. 状態の更新 ---

        // 押される前の状態を保持
        bool wasPressed = isKeyPressed || isMousePressed;

        // キーの状態を更新
        if (targetKey != KeyCode.None)
        {
            if (Input.GetKeyDown(targetKey))
                isKeyPressed = true;
            if (Input.GetKeyUp(targetKey))
                isKeyPressed = false;
        }

        // マウスの状態を更新 (0 = 左クリック)
        if (Input.GetMouseButtonDown(0))
            isMousePressed = true;
        if (Input.GetMouseButtonUp(0))
            isMousePressed = false;

        // 現在の押下状態
        bool isPressed = isKeyPressed || isMousePressed;

        // --- 2. 処理の実行 ---

        // 押された瞬間 (前は押されていなかったが、今は押されている)
        if (!wasPressed && isPressed)
        {
            // 最初の1回
            CallSEFunction();
            // タイマーをリセット
            repeatTimer = repeatDelay;
        }

        // 押され続けている間
        if (isPressed)
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
        // 押されている途中で無効になった場合に備えて、状態をリセット
        isKeyPressed = false;
        isMousePressed = false;
        repeatTimer = 0f;
    }
}
#endif