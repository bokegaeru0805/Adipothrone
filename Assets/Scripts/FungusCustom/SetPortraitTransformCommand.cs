using Fungus;
using NaughtyAttributes;
using UnityEngine;

[CommandInfo("Custom", "SetPortraitTransform", "立ち絵の一時的な向き、配置、描画順を変更します")]
public class SetPortraitTransformCommand : Command
{
    [Tooltip("変更対象のキャラクター（FungusのCharacterオブジェクトを指定します）")]
    [SerializeField]
    protected Character targetCharacter;

    [Header("Direction Settings")]
    [SerializeField]
    protected bool changeDirection = false;

    [ShowIf("changeDirection")]
    [Tooltip("チェックを入れると左向き、外すと右向きになります。")]
    [SerializeField]
    protected bool isLeft = false;

    [Header("Position Settings")]
    [SerializeField]
    protected bool changePosition = false;

    [ShowIf("changePosition")]
    [Tooltip("RectTransformのX座標を選択します。")]
    [SerializeField]
    protected PortraitPositionX positionX = PortraitPositionX.NearLeft;

    [Header("Sort Order Settings")]
    [SerializeField]
    protected bool changeSortOrder = false;

    [ShowIf("changeSortOrder")]
    [Tooltip("Heroineより前か後ろかを選択します。")]
    [SerializeField]
    protected PortraitSortOrder sortOrder = PortraitSortOrder.InFrontOfHeroine;

    public override void OnEnter()
    {
        if (targetCharacter == null)
        {
            Debug.LogWarning(
                "SetPortraitTransformCommand: 対象のキャラクターが設定されていません。",
                this
            );
            Continue();
            return;
        }

        bool controllerFound = false;

        // BasePortraitControllerで管理されている全ての起動中コントローラーから対象を検索
        foreach (var controller in BasePortraitController.ActiveControllers)
        {
            if (controller.character == targetCharacter)
            {
                // 向きの変更
                if (changeDirection)
                {
                    controller.SetDirection(isLeft);
                }

                // 配置(X座標)の変更
                if (changePosition)
                {
                    controller.SetPositionX(positionX);
                }

                // 描画順の変更
                if (changeSortOrder)
                {
                    controller.SetSortOrder(sortOrder);
                }

                controllerFound = true;
                break;
            }
        }

        if (!controllerFound)
        {
            Debug.LogWarning(
                $"SetPortraitTransformCommand: キャラクター '{targetCharacter.name}' のコントローラーが見つかりませんでした。",
                this
            );
        }

        // 次のコマンドへ進む
        Continue();
    }

    public override string GetSummary()
    {
        if (targetCharacter == null)
        {
            return "Error: キャラクターが未設定です";
        }

        if (!changeDirection && !changePosition && !changeSortOrder)
        {
            return $"{targetCharacter.name} (変更なし)";
        }

        string summary = $"{targetCharacter.name} の状態を変更";
        return summary;
    }

    public override Color GetButtonColor()
    {
        return new Color32(235, 191, 217, 255);
    }
}
