using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class PlayerBuffDebuffEffect
{
    public StatusEffectType effectType;
    public float multiplier;
    public StatusEffectRank effectrank;

    public void EffectApply()
    {
        var playerEffectManager = PlayerEffectManager.instance;
        if (playerEffectManager != null)
        {
            playerEffectManager.ApplyBuffDebuff(effectType, multiplier, effectrank);
        }
        else
        {
            Debug.LogError(
                "PlayerEffectManagerが見つかりませんでした。バフ/デバフの適用に失敗しました。"
            );
        }
    }

    // public string GetEffectName()
    // {
    //     string name = effectType switch
    //     {
    //         StatusEffectType.Attack => "攻撃力アップ",
    //         StatusEffectType.Defense => "防御力アップ",
    //         StatusEffectType.Speed => "素早さアップ",
    //         _ => "不明なバフ"
    //     };
    //     return $"{name}（{(multiplier - 1) * 100:+0;-0}%・{duration:0}秒）";
    // }
}
