using CriWare;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.Playables;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class PlayerTestMoveController : MonoBehaviour
{
    // カメラをキャッシュするための変数
    private Camera mainCamera;
    private float zOffset = 10f;

    [Header("移動設定")]
    [Tooltip("チェックを入れると矢印キーで移動、外すとマウスに追従します")]
    public bool useKeyboardInput = false;

    [Tooltip("キーボード操作時の移動速度")]
    [SerializeField, ShowIf(nameof(useKeyboardInput))]
    private float moveSpeed = 5.0f;

    [Header("ダメージテスト機能")]
    [Tooltip("1回に与えるダメージ量")]
    [SerializeField]
    private int damageAmount = 10;

    [Header("Timeline自動再生設定")]
    [Tooltip(
        "PlayableDirectorコンポーネントをアタッチして、自動再生したいTimelineを指定してください"
    )]
    [SerializeField]
    private PlayableDirector director;

    [Space(100)]
    // Playモード停止機能のための設定項目
    [Header("エディタ用デバッグ機能")]
    [Tooltip("このキーを指定回数連打するとPlayモードを停止します")]
    [SerializeField]
    private KeyCode stopKey = KeyCode.S;

    [Tooltip("Playモードを停止するために必要なキーの連打回数")]
    [SerializeField]
    private int requiredPressCount = 10;

    [Tooltip("キー連打と判定される最大の間隔（秒）")]
    [SerializeField]
    private float timeWindow = 0.5f;

    // 連打回数と時間を記録するための内部変数
    private int currentPressCount = 0;
    private float timeSinceLastPress = 0f;

    // ---　内部コンポーネント ---
    private Collider2D playerCollider;
    private CriWare.Assets.CriAtomSePlayer _sePlayer;
    private CriAtomExPlayback loopSePlayback;

    void Start()
    {
        // 効率化のため、最初にメインカメラを取得しておく
        mainCamera = Camera.main;
        // 連打カウントを初期化
        currentPressCount = 0;

        if (director != null)
        {
            director.Play();
            Debug.Log($"Timeline Started: {director.name}");
        }

        playerCollider = GetComponent<Collider2D>();
        _sePlayer = GetComponent<CriWare.Assets.CriAtomSePlayer>();
    }

    void Update()
    {
        if (useKeyboardInput)
        {
            // --- キーボード移動処理 (矢印キー or WASD) ---
            float x = Input.GetAxis("Horizontal"); // 左右キー
            float y = Input.GetAxis("Vertical"); // 上下キー

            Vector3 movement = new Vector3(x, y, 0) * moveSpeed * Time.deltaTime;
            transform.position += movement;
        }
        else
        {
            // --- 従来のマウス追従処理 ---

            // 1. マウスのスクリーン座標を取得する
            Vector3 mouseScreenPosition = Input.mousePosition;

            // 2. マウスのスクリーン座標のz座標に、カメラからの距離を設定する
            mouseScreenPosition.z = zOffset;

            // 3. スクリーン座標をワールド座標に変換する
            Vector3 mouseWorldPosition = mainCamera.ScreenToWorldPoint(mouseScreenPosition);

            // 4. オブジェクトの位置を、変換したワールド座標に設定する
            transform.position = mouseWorldPosition;
        }

        // --- ダメージテスト入力の検知（マウスクリック） ---
        if (Input.GetMouseButtonDown(0)) // 0は左クリック
        {
            ApplyDamageAtMousePosition();
        }

        // エディタ内でのみ実行するキー連打チェック処理
#if UNITY_EDITOR
        HandleEditorStop();
#endif

        if (_sePlayer != null)
        {
            if (Input.GetKeyDown(KeyCode.A))
            {
                _sePlayer.Play(SE_EnemyAction.ChargePower1);
                loopSePlayback = _sePlayer.Play(SE_Field.SawBlade);
            }
            else if (Input.GetKeyUp(KeyCode.A))
            {
                loopSePlayback.Pause();
            }
            else if (Input.GetKeyDown(KeyCode.S))
            {
                loopSePlayback.Resume(CriAtomEx.ResumeMode.AllPlayback);
            }
        }
    }

    /// <summary>
    /// マウス位置にあるオブジェクトを検出し、ダメージを与える
    /// </summary>
    private void ApplyDamageAtMousePosition()
    {
        Debug.Log("Damage Test: マウスクリック検出");

        playerCollider.enabled = false; // 一時的に自分のコライダーを無効化して自身を除外
        // マウスのスクリーン座標をワールド座標に変換
        Vector2 mouseWorldPos = mainCamera.ScreenToWorldPoint(Input.mousePosition);

        // その地点にあるCollider2Dを取得（重なっている場合は手前のもの）
        Collider2D hitCollider = Physics2D.OverlapPoint(mouseWorldPos);

        if (hitCollider != null)
        {
            // Collider2Dが見つかった場合、そのオブジェクトにIDamageableがあるかチェックしてダメージを与える
            var health = hitCollider.GetComponent<IDamageable>();

            if (health != null)
            {
                health.Damage(damageAmount);
                Debug.Log(
                    $"<color=red>Damage Test:</color> {hitCollider.name} に {damageAmount} のダメージを与えました。(CurrentHP: {health.CurrentHP})"
                );
            }
        }

        playerCollider.enabled = true; // 自分のコライダーを再度有効化
    }

    // Playモード停止を処理する専用の関数
#if UNITY_EDITOR
    private void HandleEditorStop()
    {
        // 最後のキープレスからの時間を加算
        timeSinceLastPress += Time.deltaTime;

        // もし最後のキープレスから指定時間を超えていたら、連打カウントをリセット
        if (timeSinceLastPress > timeWindow)
        {
            currentPressCount = 0;
        }

        // 指定したキーが押された瞬間を検知
        if (Input.GetKeyDown(stopKey))
        {
            currentPressCount++; // 連打カウントを1増やす
            timeSinceLastPress = 0f; // 最後のキープレスからの時間をリセット

            // 連打カウントが必要な回数に達したら
            if (currentPressCount >= requiredPressCount)
            {
                EditorApplication.isPaused = true;
            }
        }
    }
#endif
}
