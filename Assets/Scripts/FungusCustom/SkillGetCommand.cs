using System;
using Fungus;
using UnityEngine;

[CommandInfo("Skill", "Skill Get Message", "スキルの取得メッセージを表示します。")]
[AddComponentMenu("")]
public class SkillGetCommand : Command
{
    private SkillName skillID; // 表示するスキルのID

    /// <summary>
    /// GameManagerからスキルIDを流し込むためのメソッド
    /// </summary>
    public void SetSkillData(SkillName newSkillID)
    {
        skillID = newSkillID;
    }

    public override void OnEnter()
    {
        // SkillManagerから表示名を取得
        string skillDisplayName = SkillManager.instance.GetSkillDisplayName(skillID);

        // 表示テキストの組み立て
        string displayText = $"スキル「{skillDisplayName}」を手に入れた！";

        // SayDialogの準備
        SayDialog sayDialog = SayDialog.GetSayDialog();
        if (sayDialog == null)
        {
            Debug.LogError("SayDialogが見つかりません。");
            Continue();
            return;
        }

        if (sayDialog.gameObject.activeSelf == false)
        {
            sayDialog.gameObject.SetActive(true);
        }

        // キャラクター名の非表示設定
        sayDialog.SetCharacter(null);

        // ※SkillDataにiconなどのSpriteを追加していれば、ここで画像を設定することも可能です
        // sayDialog.SetCharacterImage(skillData.icon);

        // メッセージの表示実行
        sayDialog.Say(
            displayText,
            true,
            true,
            true,
            true,
            false,
            null,
            () =>
            {
                // クリック後に次のコマンドへ
                Continue();
            }
        );
    }

    public override string GetSummary()
    {
        return $"Skill Get: {skillID}";
    }

    public override Color GetButtonColor()
    {
        return new Color32(255, 235, 150, 255);
    }
}
