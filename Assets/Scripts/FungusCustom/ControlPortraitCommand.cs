using Fungus;
using UnityEngine;

[CommandInfo("Custom", "Control Portrait", "キャラクターの立ち絵を強制的に表示・非表示します。")]
[AddComponentMenu("")]
public class ControlPortraitCommand : Command
{
    public enum OperationType
    {
        Show,
        Hide,
    }

    [Tooltip("操作対象のキャラクター（FungusのCharacterオブジェクトを指定します）")]
    [SerializeField]
    protected Character targetCharacter;

    [Tooltip("表示するか非表示にするか")]
    [SerializeField]
    protected OperationType operation = OperationType.Show;

    [Tooltip("表示する立ち絵の指定文字列（例: Heroin_fat_smile）")]
    [SerializeField]
    protected string portraitString = "";

    [Tooltip("明暗の切り替えにかけるフェード時間")]
    [SerializeField]
    protected float fadeDuration = 0.25f;

    public override void OnEnter()
    {
        if (targetCharacter == null)
        {
            Debug.LogWarning("ControlPortraitCommand: 対象のキャラクターが設定されていません。");
            Continue();
            return;
        }

        bool controllerFound = false;

        foreach (var controller in BasePortraitController.ActiveControllers)
        {
            if (controller.character == targetCharacter)
            {
                if (operation == OperationType.Show)
                {
                    // 1. 指定の文字列があれば明示表示用のリクエストを呼び出す
                    if (!string.IsNullOrEmpty(portraitString))
                    {
                        controller.HandleExplicitShowRequest(portraitString);
                    }

                    // 2. 会話中（Speaking）でなければ暗くする機能の適用
                    var sayDialog = SayDialog.GetSayDialog();
                    bool isSpeaking = (
                        sayDialog != null && sayDialog.SpeakingCharacter == targetCharacter
                    );

                    // 話者なら元の明るさ、そうでないなら暗転（Dim）色
                    // Fungus標準のCharacterからDimColorを取得。なければグレー。
                    Color dimColor = new Color(0.5f, 0.5f, 0.5f, 1f);
                    Color targetColor = isSpeaking ? Color.white : dimColor;

                    // 暗転・明転の適用
                    controller.SetPortraitColorTween(targetColor, fadeDuration);
                }
                else if (operation == OperationType.Hide)
                {
                    // 非表示にする
                    controller.HandleExplicitHideRequest();
                }

                controllerFound = true;
                break;
            }
        }

        if (!controllerFound)
        {
            Debug.LogWarning(
                $"ControlPortraitCommand: キャラクター '{targetCharacter.name}' のコントローラーが見つかりませんでした。"
            );
        }

        Continue();
    }

    public override string GetSummary()
    {
        if (targetCharacter == null)
        {
            return "Error: キャラクターが未設定です";
        }

        if (operation == OperationType.Show)
        {
            return $"{targetCharacter.name} を表示 {(string.IsNullOrEmpty(portraitString) ? "(文字列なし)" : $"[{portraitString}]")}";
        }
        else
        {
            return $"{targetCharacter.name} を非表示";
        }
    }

    public override Color GetButtonColor()
    {
        // SetPortraitTransformCommandと同じピンク系の色で統一
        return new Color32(240, 160, 190, 255);
    }
}
