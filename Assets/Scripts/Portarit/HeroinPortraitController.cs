using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using Fungus;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ヒロインの立ち絵（胴体、顔、表情エフェクト、補助画像）の表示・非表示・アニメーションを管理するコントローラー。
/// Fungusのカスタムシグナル（イベント）を受信し、現在のプレイヤーの状態（体形など）に合わせて適切なスプライトを動的に合成して表示します。
/// </summary>
public class HeroinPortraitController : BasePortraitController
{
    #region Singleton & Component References

    public static HeroinPortraitController instance;

    [Header("Heroin Specific UI References")]
    [Tooltip("Immobile状態の時に表示する補助的なImage")]
    public Image immobileAuxImage;

    // 次に表示される際のImmobile状態を一時保持するフラグ
    private bool _nextIsImmobile = false;

    #endregion

    #region Unity Lifecycle Methods

    protected override void Awake()
    {
        // シングルトンの初期化
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // immobile用のImageも初期状態では非表示にする
        if (immobileAuxImage != null)
        {
            immobileAuxImage.enabled = false;
        }
        else
        {
            Debug.LogWarning("immobileAuxImageが設定されていません。");
        }

        // 基底クラスのAwake（辞書作成や初期位置記憶など）を呼び出す
        base.Awake();
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// FungusのSayコマンド等からの立ち絵表示リクエスト（イベント）を受信して処理するハンドラ。
    /// 現在のプレイヤーの体形状態を取得し、要求された表情と組み合わせてスプライト名を構築します。
    /// </summary>
    /// <param name="portraitString">Fungus側で指定されたポートレート指定文字列（例: "Heroin_Normal_Smile" など）</param>
    public override void HandleShowRequest(string portraitString)
    {
        // PlayerBodyManagerのインスタンスがない場合は体形が判定できないため処理しない
        if (PlayerBodyManager.instance == null)
        {
            Debug.LogError("PlayerBodyManagerのインスタンスが見つかりません！");
            return;
        }

        // Storyブロック（メインストーリーの会話など）でない場合は、立ち絵表示リクエストを無視する
        if (!(currentBlockType == BlockType.Story))
        {
            return;
        }

        // 指定された文字列を '_' で分割し、キャラクター名と表情名を抽出する
        // 想定フォーマット: [CharacterName]_[何か]_[ExpressionName] (例: "Heroin_A_Smile" -> "Heroin" と "Smile")
        string[] parts = portraitString.Split('_');
        if (parts.Length >= 3 && parts[0] == character.name) // 最初の部分がこのコントローラーのキャラクター名と一致するか確認
        {
            string charName = parts[0];
            string expressionString = parts.LastOrDefault(); // 配列の最後を表情名とする

            // プレイヤーの現在の体形状態（Enum）を取得し、文字列化。接頭辞の "BodyState_" を削除する。
            string bodyStateString = PlayerBodyManager
                .instance.GetCurrentBodyStateEnum()
                .ToString()
                .Replace("BodyState_", "");

            // Enum名が "Fat" などの場合、スプライト名規則に合わせるため先頭の文字だけ小文字（"fat"）に変換する
            if (!string.IsNullOrEmpty(bodyStateString))
            {
                bodyStateString = char.ToLower(bodyStateString[0]) + bodyStateString.Substring(1);
            }

            // --- immobile状態かどうかの判定 ---
            _nextIsImmobile = (bodyStateString == "immobile");

            // 最終的に検索に使用するスプライト名を組み立てる
            string bodySpriteName = $"{charName}_{bodyStateString}_body";
            string faceSpriteName = $"{charName}_{bodyStateString}_face";
            string expressionSpriteName = $"{charName}_expression_{expressionString}";

            // 基底クラスの表示メソッド（アニメーション処理付き）を呼び出す
            ShowPortrait(bodySpriteName, faceSpriteName, expressionSpriteName);
        }
        else if (parts.Length < 3)
        {
            // フォーマットが正しくない場合のみ警告を出すように変更
            Debug.LogWarning($"portraitStringのフォーマットが正しくありません: {portraitString}");
        }
    }

    /// <summary>
    /// 明暗が切り替わる際のTween処理をオーバーライドし、補助画像にも適用します。
    /// </summary>
    public override void SetPortraitColorTween(Color targetColor, float duration)
    {
        base.SetPortraitColorTween(targetColor, duration);

        if (immobileAuxImage != null)
        {
            immobileAuxImage.DOColor(targetColor, duration).SetUpdate(true);
        }
    }

    #endregion

    #region Core Display Logic Overrides

    /// <summary>
    ///  辞書からスプライトを検索し、各Imageコンポーネントに割り当てるメソッドのオーバーライド。
    ///  Heroin特有のImmobile状態（補助画像）の設定を追加します。
    /// </summary>
    protected override void SetAllSprites(
        string bodySpriteName,
        string faceSpriteName,
        string expressionSpriteName
    )
    {
        // 基底クラスの処理（胴体、顔、表情の設定）を実行
        base.SetAllSprites(bodySpriteName, faceSpriteName, expressionSpriteName);

        // --- Immobile状態（補助画像）の設定 ---
        if (immobileAuxImage != null)
        {
            immobileAuxImage.enabled = _nextIsImmobile;
        }
    }

    /// <summary>
    /// 立ち絵を即座に非表示にし、内部状態をリセットするメソッドのオーバーライド。
    /// Heroin特有の補助画像も非表示にします。
    /// </summary>
    public override void HidePortrait()
    {
        // 基底クラスの非表示処理を実行
        base.HidePortrait();

        if (immobileAuxImage != null)
        {
            immobileAuxImage.enabled = false;
        }
    }

    #endregion
}
