using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 個別のトーチ（松明）の光、アニメーション、サウンドを管理するコントローラー。
/// 外部からの指示（TorchGroupControllerなど）や、プレイヤーのエリア進入状態に応じて
/// 自身の色やSEのオンオフを自動で切り替えます。
/// </summary>
[RequireComponent(typeof(CriWare.Assets.CriAtomSePlayer))]
public class TorchController : MonoBehaviour
{
    /// <summary>
    /// トーチの現在の状態を表す列挙型
    /// </summary>
    public enum TorchState
    {
        None = 0, // 未設定・初期化前
        Off = 10, // 消灯
        Red = 20, // 赤い炎で点灯
        Blue = 30, // 青い炎で点灯
    }

    #region Inspector Settings

    [Header("基本設定")]
    [Tooltip("シーン開始時のトーチの初期状態")]
    [SerializeField]
    public TorchState firstState = TorchState.Red;

    [Tooltip(
        "このトーチが配置されているエリア。プレイヤーがこのエリアにいる時だけ燃焼音が鳴ります。"
    )]
    [SerializeField]
    private CameraMoveArea targetCameraArea;

    [Tooltip("消灯時に表示されるベースとなるスプライト（火がついていない状態の画像）")]
    [SerializeField]
    private Sprite defaultTorchSprite = null;

    [Header("サウンド設定")]
    [Tooltip(
        "このトーチから音（着火音・ループ燃焼音）を鳴らすかどうか。\nゲーム進行中に外部のスクリプトやFungusコマンドから変更することも可能です。"
    )]
    public bool enableSE = true;

    #endregion

    #region Private Fields

    // --- 状態管理フラグ ---
    private bool isFirstUpdate = true; // 初回起動時の状態セットによるSE再生を防ぐためのフラグ
    private bool isPlayerInTargetArea = false; // プレイヤーが同じエリア内にいるかどうか
    private TorchState currentState = TorchState.None; // 現在のトーチの状態

    // --- コンポーネントキャッシュ ---
    private Light2D torchLight;
    private Animator torchAnimator;
    private Material torchMaterial;
    private CriWare.Assets.CriAtomSePlayer sePlayer;

    // --- 色の定義 ---
    private readonly Color redTorchColor = new Color(1f, 0.35f, 0.052f);
    private readonly Color blueTorchColor = new Color(0.051f, 0.78f, 1f);

    #endregion

    #region Unity Lifecycle Methods

    private void Awake()
    {
        // 必須コンポーネントの取得
        torchLight = GetComponent<Light2D>();
        torchAnimator = GetComponent<Animator>();
        torchMaterial = GetComponent<SpriteRenderer>().material;
        sePlayer = GetComponent<CriWare.Assets.CriAtomSePlayer>();

        if (targetCameraArea == null)
        {
            Debug.LogError(
                $"[{name}] targetCameraArea が設定されていません。環境音の制御が正しく行われません。",
                this
            );
        }
    }

    private void Start()
    {
        // Start時は「シーンロード直後」なので、着火SEを鳴らさずに初期状態だけを静かにセットする
        SetTorchState(firstState, false);

        // 初回セットアップ完了
        isFirstUpdate = false;
        isPlayerInTargetArea = false;
    }

    private void OnEnable()
    {
        // エリア進入・退出イベントの購読
        CameraMoveArea.OnPlayerEnteredArea += HandlePlayerEnteredArea;
        CameraMoveArea.OnPlayerExitedArea += HandlePlayerExitedArea;
    }

    private void OnDisable()
    {
        // エリア進入・退出イベントの購読解除（メモリリーク防止）
        CameraMoveArea.OnPlayerEnteredArea -= HandlePlayerEnteredArea;
        CameraMoveArea.OnPlayerExitedArea -= HandlePlayerExitedArea;
    }

    private void Update()
    {
        // ------------------------------------------------------------
        // 環境音（燃焼音）のループ制御
        // ------------------------------------------------------------
        // 条件: プレイヤーが同じエリアにいる かつ SE再生が許可されている
        if (isPlayerInTargetArea && enableSE)
        {
            // 音が鳴っておらず、かつ「消灯」や「未設定」ではない（＝燃えている）場合
            if (
                !sePlayer.IsPlaying()
                && currentState != TorchState.Off
                && currentState != TorchState.None
            )
            {
                sePlayer.Play(SE_Field.FireBurning1); // 燃える音をループ再生開始
            }
        }
        else
        {
            // エリア外に出た、または強制的にSEが禁止された場合は音を即座に止める
            if (sePlayer.IsPlaying())
            {
                sePlayer.Stop();
            }
        }
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// プレイヤーが任意のエリアに進入した際に呼ばれるハンドラ。
    /// 自分の設定されたエリアであればフラグを立てます。
    /// </summary>
    private void HandlePlayerEnteredArea(CameraMoveArea enteredArea)
    {
        if (enteredArea == targetCameraArea)
        {
            isPlayerInTargetArea = true;
        }
    }

    /// <summary>
    /// プレイヤーが任意のエリアから退出した際に呼ばれるハンドラ。
    /// 自分の設定されたエリアであればフラグを折ります。
    /// </summary>
    private void HandlePlayerExitedArea(CameraMoveArea exitedArea)
    {
        if (exitedArea == targetCameraArea)
        {
            isPlayerInTargetArea = false;
        }
    }

    #endregion

    #region Public Methods

    // =========================================================
    // ▼ 外部スクリプトやUnity Eventから個別に操作するためのメソッド群 ▼
    // =========================================================

    public void TurnOnRed(bool playSE = true) => SetTorchState(TorchState.Red, playSE);

    public void TurnOnBlue(bool playSE = true) => SetTorchState(TorchState.Blue, playSE);

    public void TurnOff(bool playSE = true) => SetTorchState(TorchState.Off, playSE);

    /// <summary>
    /// トーチの光、アニメーション、材質、SEを統合して指定した状態へ変更します。
    /// </summary>
    /// <param name="torchState">変更先の状態（Red, Blue, Off）</param>
    /// <param name="playSE">状態変化時（着火・消火）の瞬間的なSEを鳴らすか（デフォルトはtrue）</param>
    public void SetTorchState(TorchState torchState, bool playSE = true)
    {
        // 既に同じ状態なら無駄な処理を省く
        if (torchState == currentState)
        {
            return;
        }

        currentState = torchState;

        // SEを鳴らす条件:
        // 1. 引数で鳴らすように指示されている (playSE == true)
        // 2. コンポーネント自体でSEが許可されている (enableSE == true)
        // 3. ゲーム開始直後の初期化タイミングではない (!isFirstUpdate)
        bool shouldPlaySE = playSE && enableSE && !isFirstUpdate;

        switch (torchState)
        {
            case TorchState.Off:
                torchLight.enabled = false;
                torchAnimator.enabled = false;
                torchMaterial.DisableKeyword("HSV_ON"); // シェーダーの光エフェクトを切る
                torchMaterial.mainTexture = defaultTorchSprite.texture; // 通常の消灯スプライトに戻す

                if (shouldPlaySE)
                    sePlayer.Play(SE_Field.FlameOff);
                break;

            case TorchState.Red:
                torchLight.enabled = true;
                torchAnimator.enabled = true;
                torchLight.color = redTorchColor;
                torchMaterial.EnableKeyword("HSV_ON"); // シェーダーの光エフェクトを入れる
                torchAnimator.SetTrigger("red");

                if (shouldPlaySE)
                    sePlayer.Play(SE_Field.FlameOn);
                break;

            case TorchState.Blue:
                torchLight.enabled = true;
                torchAnimator.enabled = true;
                torchLight.color = blueTorchColor;
                torchMaterial.DisableKeyword("HSV_ON"); // 青はHSVエフェクトを使用しない
                torchAnimator.SetTrigger("blue");

                if (shouldPlaySE)
                    sePlayer.Play(SE_Field.FlameOn);
                break;
        }
    }

    #endregion
}
