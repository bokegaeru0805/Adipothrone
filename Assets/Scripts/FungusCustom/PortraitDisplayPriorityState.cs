using System.Collections.Generic;
using System.Linq;
using Fungus;
using UnityEngine;

/// <summary>
/// 会話中の画像表示優先度を解決し、キャラクター単位の一時上書きを管理します。
/// </summary>
public static class PortraitDisplayPriorityState
{
    private static readonly Dictionary<Character, PortraitDisplayPriority> CharacterOverrides =
        new Dictionary<Character, PortraitDisplayPriority>();
    private static PortraitDisplayPriority? _globalOverride;

    public static PortraitDisplayPriority Resolve(Block block, Character character)
    {
        if (character != null && CharacterOverrides.TryGetValue(character, out var priority))
        {
            return priority;
        }

        if (_globalOverride.HasValue)
        {
            return _globalOverride.Value;
        }

        return block != null
            ? block.PortraitPriority
            : PortraitDisplayPriority.FaceGraphicFirst;
    }

    public static void SetOverride(Character character, PortraitDisplayPriority priority)
    {
        if (character != null)
        {
            CharacterOverrides[character] = priority;
        }
    }

    public static void ClearOverride(Character character)
    {
        if (character != null)
        {
            CharacterOverrides.Remove(character);
        }
    }

    /// <summary>
    /// 全キャラクターへ強制設定を適用します。
    /// 実行時点で既存の個別設定を解除し、以後に設定された個別指定だけを例外として扱います。
    /// </summary>
    public static void SetGlobalOverride(PortraitDisplayPriority priority)
    {
        CharacterOverrides.Clear();
        _globalOverride = priority;
    }

    /// <summary>
    /// 全体・個別の強制設定を解除し、Block設定へ戻します。
    /// </summary>
    public static void ClearAllOverrides()
    {
        CharacterOverrides.Clear();
        _globalOverride = null;
    }

    public static void ResetOverrides()
    {
        ClearAllOverrides();
    }

    public static BasePortraitController FindDynamicPortraitController(Character character)
    {
        if (character == null)
        {
            return null;
        }

        foreach (var controller in BasePortraitController.ActiveControllers)
        {
            if (controller != null && controller.character == character)
            {
                return controller;
            }
        }

        return null;
    }

    /// <summary>
    /// 顔グラフィック優先時に使用するSpriteを返します。
    /// Heroinは現在の体型とCSVの表情指定から顔グラフィック名を組み立てます。
    /// </summary>
    public static Sprite ResolveFaceGraphic(
        Character character,
        string portraitString,
        Sprite configuredPortrait
    )
    {
        if (character == null || string.IsNullOrEmpty(portraitString))
        {
            return configuredPortrait;
        }

        string portraitName = portraitString;
        if (character.gameObject.name == "Heroin")
        {
            string[] portraitParts = portraitString.Split('_');
            string expressionName = portraitParts.LastOrDefault();
            if (string.IsNullOrEmpty(expressionName))
            {
                return null;
            }

            // CSVのanxious表記と顔グラフィック素材のanxiety表記を合わせる。
            if (expressionName == "anxious")
            {
                expressionName = "anxiety";
            }

            string bodyStateName = "normal";
            if (PlayerBodyManager.instance != null)
            {
                bodyStateName = PlayerBodyManager.instance
                    .GetCurrentBodyStateEnum()
                    .ToString()
                    .Replace("BodyState_", "");
                bodyStateName = char.ToLower(bodyStateName[0]) + bodyStateName.Substring(1);
            }

            portraitName = $"Heroin_{bodyStateName}_{expressionName}";
        }

        Sprite resolvedPortrait = character.Portraits.FirstOrDefault(
            portrait => portrait != null && portrait.name == portraitName
        );
        return resolvedPortrait != null ? resolvedPortrait : configuredPortrait;
    }
}
