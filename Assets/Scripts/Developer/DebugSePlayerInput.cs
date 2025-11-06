using System.Collections;
using UnityEngine;

/// <summary>
/// キーボード入力に応じてSEを再生するためのデバッグ用コンポーネントです。
/// CriAtomSePlayerがアタッチされているGameObjectに追加して使用します。
/// </summary>
// このコンポーネントはCriAtomSePlayerが必須であることを示します。
// もしCriAtomSePlayerがない場合、自動的に追加されます。
[RequireComponent(typeof(CriWare.Assets.CriAtomSePlayer))]
public class DebugSePlayerInput : MonoBehaviour
{
    // SE再生を行う本体コンポーネントへの参照
    private CriWare.Assets.CriAtomSePlayer sePlayer;

    [Header("自身のSEを再生するキー")]
    [Tooltip(
        "このキーを押すと、同じGameObjectのCriAtomSePlayerに設定されているCue NameのSEが再生されます。"
    )]
    [SerializeField]
    private KeyCode playOwnCueKey = KeyCode.None;

    [Header("別のSEを再生するキーマッピング")]
    [Tooltip(
        "リストにキーとキュー名を設定すると、そのキーを押した時に指定した名前のSEが再生されます。"
    )]
    [SerializeField]
    private KeyCueMapping[] alternateCues;

    [Header("ピッチ変更設定")]
    [Tooltip("マウスホイール1単位あたりのピッチ変化量（セント単位）。100で半音、1200で1オクターブ。")]
    [SerializeField] private float pitchStep = 100f; // デフォルトは半音ずつ

    [Tooltip("ピッチの最小値（セント単位）")]
    [SerializeField] private float minPitch = -1200f; // デフォルトは1オクターブ下

    [Tooltip("ピッチの最大値（セント単位）")]
    [SerializeField] private float maxPitch = 1200f; // デフォルトは1オクターブ上

    // 現在のピッチを保持する内部変数（セント単位）
    private float currentPitch = 0f;

    /// <summary>
    /// キーとキュー名の対応を定義するためのクラスです。
    /// [System.Serializable]を付けることで、インスペクター上にリストとして表示できるようになります。
    /// </summary>
    [System.Serializable]
    public class KeyCueMapping
    {
        [Tooltip("トリガーとなるキー")]
        public KeyCode key = KeyCode.None;

        [Tooltip("このキーで再生するキュー名")]
        public string cueName = "";
    }

    /// <summary>
    /// ゲームオブジェクトが有効になった最初のフレームで呼び出されます。
    /// </summary>
    void Awake()
    {
        // 同じGameObjectにアタッチされているCriAtomSePlayerコンポーネントを取得します。
        sePlayer = GetComponent<CriWare.Assets.CriAtomSePlayer>();
        currentPitch = 0f;
        ApplyPitch();
    }

    /// <summary>
    /// 毎フレーム呼び出されます。
    /// </summary>
    void Update()
    {
        // 1. 自身のSEを再生するキーのチェック
        //    キーが設定されており(Noneではなく)、かつ、そのキーが押された瞬間のフレームであるかを確認します。
        if (playOwnCueKey != KeyCode.None && Input.GetKeyDown(playOwnCueKey))
        {
            // sePlayerのPlay()メソッドを呼び出し、自身のキュー名で再生します。
            Debug.Log($"Play Own Cue: '{sePlayer.cueName}' with key '{playOwnCueKey}'");
            sePlayer.Play();
        }

        // 2. 別のSEを再生するキーマッピングのチェック
        //    リストがnullでなく、要素が存在する場合のみ処理を行います。
        if (alternateCues != null && alternateCues.Length > 0)
        {
            // リスト内の各マッピング情報を一つずつチェックします。
            foreach (var mapping in alternateCues)
            {
                // マッピングにキーが設定されており(Noneではなく)、そのキーが押された瞬間かを確認します。
                if (mapping.key != KeyCode.None && Input.GetKeyDown(mapping.key))
                {
                    // sePlayerのPlay(string)メソッドを使い、マッピングで指定されたキュー名で再生します。
                    Debug.Log($"Play Alternate Cue: '{mapping.cueName}' with key '{mapping.key}'");
                    sePlayer.Play(mapping.cueName);
                }
            }
        }

        float scrollInput = Input.GetAxis("Mouse ScrollWheel");
        if (scrollInput != 0)
        {
            // 現在のピッチに変化量を加算
            currentPitch += scrollInput * pitchStep;
            // ピッチを最小値と最大値の間に制限（クランプ）
            currentPitch = Mathf.Clamp(currentPitch, minPitch, maxPitch);
            // 変更したピッチを CriAtomExPlayer に適用
            ApplyPitch();
            // 現在のピッチをログに出力
            Debug.Log($"Pitch changed: {currentPitch:F0} cents");
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            // CustomApplyPitch();
            sePlayer.Stop();
        }
    }

    // 現在のピッチ値をCriAtomExPlayerに適用します。
    /// </summary>
    private void ApplyPitch()
    {
        // CriAtomSePlayer が内部で管理している CriAtomExPlayer を直接取得してピッチを設定
        // CriAtomSePlayer自体にはピッチを設定する直接的なメソッドがないため
        if (sePlayer != null && sePlayer.player != null)
        {
            sePlayer.player.SetPitch(currentPitch);
            // ピッチ変更を即座に反映させるためにUpdateAll()を呼ぶことが推奨される場合がある
            // sePlayer.player.UpdateAll();
        }
    }

    // private void CustomApplyPitch()
    // {
    //     StartCoroutine(CustomApplyPitchCoroutine());
    // }
    
    // private IEnumerator CustomApplyPitchCoroutine()
    // {
    //    // CriAtomSePlayer が内部で管理している CriAtomExPlayer を直接取得してピッチを設定
    //     // CriAtomSePlayer自体にはピッチを設定する直接的なメソッドがないため
    //     if (sePlayer != null && sePlayer.player != null)
    //     {
    //         sePlayer.Play();
    //         sePlayer.player.SetPitch(currentPitch);
    //         yield return new WaitForSeconds(1f); // 少し待つ
    //         sePlayer.Play();
    //         // ピッチ変更を即座に反映させるためにUpdateAll()を呼ぶことが推奨される場合がある
    //         // sePlayer.player.UpdateAll();
    //     }
    // }
}