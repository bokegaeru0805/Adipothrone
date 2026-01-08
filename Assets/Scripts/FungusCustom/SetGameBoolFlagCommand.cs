using Fungus;
using NaughtyAttributes;
using UnityEngine;

/// <summary>
/// ゲーム内のあらゆるBool型フラグ（進行フラグ）を設定するための統合Fungusコマンド。
/// FlagData.cs で定義された各章のEnumを包括的に扱います。
/// </summary>
[CommandInfo("Flag", "Set Game Bool Flag", "指定した章のBoolフラグの値を変更します")]
public class SetGameBoolFlagCommand : Command
{
    // どの章のフラグを操作するか選ぶためのEnum
    public enum FlagCategory
    {
        Tutorial,
        Prologue,
        Chapter1,
        Chapter2,
        // 新しい章（Chapter3など）を追加する場合はここに追記してください
    }

    [Tooltip("操作するフラグのカテゴリ（章）")]
    [SerializeField]
    private FlagCategory category = FlagCategory.Tutorial;

    // --- 各章ごとのフラグ変数 ---
    // NaughtyAttributesの [ShowIf] を使って、categoryの選択に応じて表示/非表示を切り替えます
    // [Label] を使って、Inspector上の表示名を "Flag Name" に統一しています

    [SerializeField]
    [AllowNesting]
    [ShowIf("category", FlagCategory.Tutorial)]
    [Label("Flag Name")]
    private TutorialEvent tutorialFlag;

    [SerializeField]
    [AllowNesting]
    [ShowIf("category", FlagCategory.Prologue)]
    [Label("Flag Name")]
    private PrologueTriggeredEvent prologueFlag;

    [SerializeField]
    [AllowNesting]
    [ShowIf("category", FlagCategory.Chapter1)]
    [Label("Flag Name")]
    private Chapter1TriggeredEvent chapter1Flag;

    [SerializeField]
    [AllowNesting]
    [ShowIf("category", FlagCategory.Chapter2)]
    [Label("Flag Name")]
    private Chapter2TriggeredEvent chapter2Flag;

    // ---------------------------

    [Tooltip("フラグに設定したい値 (true/false)")]
    [SerializeField]
    private bool valueToSet = true;

    // このコマンドが実行されたときに呼ばれる処理
    public override void OnEnter()
    {
        if (FlagManager.instance == null)
        {
            Debug.LogError("FlagManagerが見つかりません！");
            Continue();
            return;
        }

        // カテゴリに応じて、適切なEnumをFlagManagerに渡す
        switch (category)
        {
            case FlagCategory.Tutorial:
                FlagManager.instance.SetBoolFlag(tutorialFlag, valueToSet);
                break;
            case FlagCategory.Prologue:
                FlagManager.instance.SetBoolFlag(prologueFlag, valueToSet);
                break;
            case FlagCategory.Chapter1:
                FlagManager.instance.SetBoolFlag(chapter1Flag, valueToSet);
                break;
            case FlagCategory.Chapter2:
                FlagManager.instance.SetBoolFlag(chapter2Flag, valueToSet);
                break;
        }

        // 次のコマンドへ処理を続ける
        Continue();
    }

    public override string GetSummary()
    {
        // Flowchart上で「どのフラグを何にしたか」が一目でわかるように要約を表示
        string flagName = "";
        switch (category)
        {
            case FlagCategory.Tutorial:
                flagName = tutorialFlag.ToString();
                break;
            case FlagCategory.Prologue:
                flagName = prologueFlag.ToString();
                break;
            case FlagCategory.Chapter1:
                flagName = chapter1Flag.ToString();
                break;
            case FlagCategory.Chapter2:
                flagName = chapter2Flag.ToString();
                break;
        }

        return $"{category}: {flagName} = {valueToSet}";
    }

    public override Color GetButtonColor()
    {
        // 汎用コマンドなので、共通の色（ベージュ/オレンジ系）を設定
        return new Color32(251, 207, 153, 255);
    }
}
