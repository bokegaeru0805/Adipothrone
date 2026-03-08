using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// SEのenumとキュー名（string）のマッピングを管理する静的クラス（データベース）
/// </summary>
public static class SeCueDatabase
{
    // --- 内部データ（privateに変更） ---

    /// <summary>
    /// UIカテゴリのSEとキュー名マッピング
    /// </summary>
    private static readonly Dictionary<SE_UI, string> seNameTable_UI = new Dictionary<SE_UI, string>
    {
        { SE_UI.Beep1, "Beep1" },
        { SE_UI.Complete1, "Complete1" },
        { SE_UI.Decision1, "Decision1" },
        { SE_UI.WeaponDecision1, "WeaponDecision1" },
        { SE_UI.Register1, "Register1" },
        { SE_UI.DialogVoice, "DialogVoice" },
    };

    /// <summary>
    /// プレイヤーアクションカテゴリのSEとキュー名マッピング
    /// </summary>
    private static readonly Dictionary<SE_PlayerAction, string> seNameTable_PlayerAction =
        new Dictionary<SE_PlayerAction, string>
        {
            { SE_PlayerAction.Boot1, "Boot1" },
            { SE_PlayerAction.Bound1, "Bound1" },
            { SE_PlayerAction.Bound2, "Bound2" },
            { SE_PlayerAction.Bound3, "Bound3" },
            { SE_PlayerAction.Damage1, "Damage1" },
            { SE_PlayerAction.Eat1, "Eat1" },
            { SE_PlayerAction.GichiGichi1, "GichiGichi1" },
            { SE_PlayerAction.MuchiMuchi, "MuchiMuchi" },
            { SE_PlayerAction.Shoot1_Player, "Shoot1_Player" },
            { SE_PlayerAction.ShockWave1, "ShockWave1" },
            { SE_PlayerAction.Swing1, "Swing1" },
            { SE_PlayerAction.Swing2, "Swing2" },
            { SE_PlayerAction.Walk1, "Walk1" },
            { SE_PlayerAction.SoftBounce1, "SoftBounce1" },
            { SE_PlayerAction.Jump1, "Jump1" },
            { SE_PlayerAction.Land1, "Land1" },
            { SE_PlayerAction.HealItem1, "HealItem1" },
            { SE_PlayerAction.Hit1, "Hit1" },
            { SE_PlayerAction.Buff1, "Buff1" },
            { SE_PlayerAction.AttackMiss1, "AttackMiss1" },
            { SE_PlayerAction.Death1, "Death1" },
            { SE_PlayerAction.FallDown1, "FallDown1" },
        };

    /// <summary>
    /// 敵アクションカテゴリのSEとファイル名マッピング
    /// </summary>
    private static readonly Dictionary<SE_EnemyAction, string> seNameTable_EnemyAction =
        new Dictionary<SE_EnemyAction, string>
        {
            { SE_EnemyAction.ChargePower1, "ChargePower1" },
            { SE_EnemyAction.Damage2, "Attack_player1" },
            { SE_EnemyAction.FastMove1, "FastMove1" },
            { SE_EnemyAction.Roar1, "Roar1" },
            { SE_EnemyAction.Shoot1_Enemy, "Shoot1_Enemy" },
            { SE_EnemyAction.Shoot2_Enemy, "Shoot2_Enemy" },
            { SE_EnemyAction.Shoot_Water1, "Shoot_Water1" },
            { SE_EnemyAction.Impact_iron1, "Impact_iron1" },
            { SE_EnemyAction.Attack_slime1, "Attack_slime1" },
            { SE_EnemyAction.Attack_slime_boss, "Attack_slime_boss" },
            { SE_EnemyAction.Attack_fly1, "Attack_fly1" },
            { SE_EnemyAction.Kick1, "Kick1" },
            { SE_EnemyAction.Land_enemy1, "Land_enemy1" },
            { SE_EnemyAction.MagicWave1, "MagicWave1" },
            { SE_EnemyAction.SwordSlash1, "SwordSlash1" },
            { SE_EnemyAction.SwordSlash2, "SwordSlash2" },
            { SE_EnemyAction.SwordSlash3, "SwordSlash3" },
            { SE_EnemyAction.SwordThrow1, "SwordThrow1" },
            { SE_EnemyAction.RareEnemyAppear, "RareEnemyAppear" },
            { SE_EnemyAction.Death1, "Death1_Enemy" },
            { SE_EnemyAction.Walk1, "Walk1_Enemy" },
            { SE_EnemyAction.Attack_wind1, "Attack_wind1" },
            { SE_EnemyAction.Attack_throw1, "Attack_throw1" },
            { SE_EnemyAction.SandEmerge, "SandEmerge" },
            { SE_EnemyAction.SandSubmerge, "SandSubmerge" },
            { SE_EnemyAction.Drop_Metal, "Drop_Metal" },
            { SE_EnemyAction.GearTurn, "GearTurn" },
            { SE_EnemyAction.LaserAttack1, "LaserExpand2" },
            { SE_EnemyAction.Spawn1, "Spawn1" },
        };

    /// <summary>
    /// 環境カテゴリのSEとファイル名マッピング
    /// </summary>
    private static readonly Dictionary<SE_Field, string> seNameTable_Field = new Dictionary<
        SE_Field,
        string
    >
    {
        { SE_Field.DoorLock, "DoorLock" },
        { SE_Field.DoorOpen_Metal, "DoorOpen_Metal" },
        { SE_Field.DoorOpenLock, "DoorOpenLock" },
        { SE_Field.OpenTreasurebox1, "OpenTreasurebox1" },
        { SE_Field.Collapse1, "Collapse1" },
        { SE_Field.Collapse2, "Collapse2" },
        { SE_Field.Collapse3, "Collapse3" },
        { SE_Field.SmallBomb, "SmallBomb1" },
        { SE_Field.SmallCollapse, "SmallCollapse1" },
        { SE_Field.SwitchOn, "SwitchOn" },
        { SE_Field.WaterDrip1, "WaterDrip1" },
        { SE_Field.WaterDrop1, "WaterDrop1" },
        { SE_Field.CoinGet1, "CoinGet1" },
        { SE_Field.FlameOn, "FlameOn" },
        { SE_Field.FlameOff, "FlameOff" },
        { SE_Field.GroundRumble1, "GroundRumble1" },
        { SE_Field.LiftMove_Wood, "LiftMove_Wood" },
        { SE_Field.FireBurning1, "FireBurning1" },
        { SE_Field.WindGust_weak, "WindGust_weak" },
        { SE_Field.WindGust_strong, "WindGust_strong" },
        { SE_Field.Sand1, "Sand1" },
        { SE_Field.Clash_WaterGlass, "Clash_WaterGlass" },
        { SE_Field.WaterMove1, "WaterMove1" },
        { SE_Field.LaserShoot, "LaserShoot" },
        { SE_Field.LaserExpand, "LaserExpand" },
        { SE_Field.SawBlade, "SawBlade" },
        { SE_Field.HorseWalk, "Walk_Horse" },
        { SE_Field.HorseCarriage, "HorseCarriage" },
        { SE_Field.InsertDevice1, "InsertDevice1" },
    };

    /// <summary>
    /// システムイベントカテゴリのSEとファイル名マッピング
    /// </summary>
    private static readonly Dictionary<SE_SystemEvent, string> seNameTable_SystemEvent =
        new Dictionary<SE_SystemEvent, string>
        {
            { SE_SystemEvent.Impact1, "Impact1" },
            { SE_SystemEvent.Quake, "Quake1" },
            { SE_SystemEvent.Vanish1, "Vanish1" },
            { SE_SystemEvent.Warning1, "Warning1" },
            { SE_SystemEvent.Warp1, "Warp1" },
            { SE_SystemEvent.WarpStandby1, "WarpStandby1" },
            { SE_SystemEvent.ItemGet1, "ItemGet1" },
            { SE_SystemEvent.ItemGet2, "ItemGet2" },
            { SE_SystemEvent.Effect_Buff, "Effect_Buff" },
            { SE_SystemEvent.CashRegister, "CashRegister" },
            { SE_SystemEvent.LevelUp, "LevelUp" },
            { SE_SystemEvent.Flash1, "Flash1" },
        };

    // --- 辞書管理機能（ここからがロジック本体） ---

    /// <summary>
    /// 全てのカテゴリの辞書を統合した、単一の辞書
    /// </summary>
    private static Dictionary<Enum, string> _unifiedCueTable;

    /// <summary>
    /// ゲーム開始時に自動で辞書を初期化します。
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        _unifiedCueTable = new Dictionary<Enum, string>();

        // UIカテゴリを登録
        foreach (var pair in seNameTable_UI)
        {
            if (_unifiedCueTable.ContainsKey(pair.Key))
            {
                Debug.LogWarning($"[SeCueDatabase] 重複キー: {pair.Key}");
                continue;
            }
            _unifiedCueTable.Add(pair.Key, pair.Value);
        }

        // プレイヤーアクションカテゴリを登録
        foreach (var pair in seNameTable_PlayerAction)
        {
            if (_unifiedCueTable.ContainsKey(pair.Key))
            {
                Debug.LogWarning($"[SeCueDatabase] 重複キー: {pair.Key}");
                continue;
            }
            _unifiedCueTable.Add(pair.Key, pair.Value);
        }

        // 敵アクションカテゴリを登録
        foreach (var pair in seNameTable_EnemyAction)
        {
            if (_unifiedCueTable.ContainsKey(pair.Key))
            {
                Debug.LogWarning($"[SeCueDatabase] 重複キー: {pair.Key}");
                continue;
            }
            _unifiedCueTable.Add(pair.Key, pair.Value);
        }

        // 環境カテゴリを登録
        foreach (var pair in seNameTable_Field)
        {
            if (_unifiedCueTable.ContainsKey(pair.Key))
            {
                Debug.LogWarning($"[SeCueDatabase] 重複キー: {pair.Key}");
                continue;
            }
            _unifiedCueTable.Add(pair.Key, pair.Value);
        }

        // システムイベントカテゴリを登録
        foreach (var pair in seNameTable_SystemEvent)
        {
            if (_unifiedCueTable.ContainsKey(pair.Key))
            {
                Debug.LogWarning($"[SeCueDatabase] 重複キー: {pair.Key}");
                continue;
            }
            _unifiedCueTable.Add(pair.Key, pair.Value);
        }
    }

    /// <summary>
    /// 指定されたenumに対応するキュー名（string）を取得します。
    /// </summary>
    /// <param name="cue">SEのenum</param>
    /// <returns>対応するキュー名。見つからない場合はnull。</returns>
    public static string GetCueName(Enum cue)
    {
        // アプリ起動後、万が一辞書が初期化されていなかった場合（※ほぼ発生しないが念のため）
        if (_unifiedCueTable == null)
        {
            Debug.LogError("[SeCueDatabase] 辞書が初期化されていません。");
            Initialize(); // 強制的に初期化
        }

        if (_unifiedCueTable.TryGetValue(cue, out string cueName))
        {
            return cueName;
        }
        else
        {
            Debug.LogWarning($"[SeCueDatabase] キュー '{cue}' が辞書に登録されていません。");
            return null;
        }
    }
}
