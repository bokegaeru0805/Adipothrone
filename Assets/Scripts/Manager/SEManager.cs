using System;
using System.Collections.Generic;
using UnityEngine;

public class SEManager : MonoBehaviour
{
    public static SEManager instance;
    private Dictionary<string, AudioSource> seDictionary = new Dictionary<string, AudioSource>();
    private Dictionary<string, float> originalVolumes = new Dictionary<string, float>(); // 追加：初期音量保存用
    private Dictionary<string, float> originalPitchs = new Dictionary<string, float>(); // 追加：初期ピッチ保存用
    private Dictionary<string, float> originalLenght = new Dictionary<string, float>(); //追加:初期長さ保存用

    /// <summary>
    /// UIカテゴリのSEとファイル名マッピング
    /// </summary>
    private static readonly Dictionary<SE_UI, string> seNameTable_UI = new Dictionary<SE_UI, string>
    {
        { SE_UI.Beep1, "Beep1" },
        { SE_UI.Complete1, "Complete1" },
        { SE_UI.Decision1, "Decision1" },
        { SE_UI.WeaponDecision1, "WeaponDecision1" },
    };

    /// <summary>
    /// プレイヤーアクションカテゴリのSEとファイル名マッピング
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
        };

    /// <summary>
    /// 敵アクションカテゴリのSEとファイル名マッピング
    /// </summary>
    private static readonly Dictionary<SE_EnemyAction, string> seNameTable_EnemyAction =
        new Dictionary<SE_EnemyAction, string>
        {
            { SE_EnemyAction.ChargePower1, "ChargePower1" },
            { SE_EnemyAction.Damage2, "Damage2" },
            { SE_EnemyAction.FastMove1, "FastMove1" },
            { SE_EnemyAction.Roar1, "Roar1" },
            { SE_EnemyAction.Shoot1_Enemy, "Shoot1_Enemy" },
            { SE_EnemyAction.Shoot2_Enemy, "Shoot2_Enemy" },
            { SE_EnemyAction.Impact_iron1, "Impact_iron1" },
            { SE_EnemyAction.Attack_slime1, "Attack_slime1" },
            { SE_EnemyAction.Attack_fly1, "Attack_fly1" },
            { SE_EnemyAction.Kick1, "Kick1" },
            { SE_EnemyAction.Land_enemy1, "Land_enemy1" },
            { SE_EnemyAction.MagicWave1, "MagicWave1" },
            { SE_EnemyAction.SwordSlash1, "SwordSlash1" },
            { SE_EnemyAction.SwordThrow1, "SwordThrow1" },
            { SE_EnemyAction.RareEnemyAppear, "RareEnemyAppear" },
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
        { SE_Field.SmallBomb, "SmallBomb" },
        { SE_Field.SmallCollapse, "SmallCollapse" },
        { SE_Field.SwitchOn, "SwitchOn" },
        { SE_Field.WaterDrip1, "WaterDrip1" },
        { SE_Field.WaterDrop1, "WaterDrop1" },
        { SE_Field.CoinGet1, "CoinGet1" },
        { SE_Field.FlameOn, "FlameOn" },
        { SE_Field.FlameOff, "FlameOff" },
        { SE_Field.GroundRumble1, "GroundRumble1" },
    };

    /// <summary>
    /// システムイベントカテゴリのSEとファイル名マッピング
    /// </summary>
    private static readonly Dictionary<SE_SystemEvent, string> seNameTable_SystemEvent =
        new Dictionary<SE_SystemEvent, string>
        {
            { SE_SystemEvent.Impact1, "Impact1" },
            { SE_SystemEvent.Quake, "Quake" },
            { SE_SystemEvent.Vanish1, "Vanish1" },
            { SE_SystemEvent.Warning1, "Warning1" },
            { SE_SystemEvent.Warp1, "Warp1" },
            { SE_SystemEvent.WarpStandby1, "WarpStandby1" },
            { SE_SystemEvent.ItemGet1, "ItemGet1" },
            { SE_SystemEvent.ItemGet2, "ItemGet2" },
            { SE_SystemEvent.Effect_Buff, "Effect_Buff" },
            { SE_SystemEvent.CashRegister, "CashRegister" },
            { SE_SystemEvent.LevelUp, "LevelUp" },
        };

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            //DontDestroyOnLoad(this.gameObject);
            // 子オブジェクトの AudioSource をすべて登録
            foreach (Transform child in transform)
            {
                AudioSource source = child.GetComponent<AudioSource>();
                if (source != null)
                {
                    seDictionary[child.name] = source;
                    originalVolumes[child.name] = source.volume; // 初期音量を保存
                    originalPitchs[child.name] = source.pitch; // 初期ピッチを保存
                    originalLenght[child.name] = source.clip.length; //初期長さを保存
                }
            }
        }
        else
        {
            Destroy(this.gameObject);
        }
    }

    private void Start()
    {
        // SEの音量を設定
        if (SaveLoadManager.instance != null)
        {
            float seVolume = SaveLoadManager.instance.Settings.seVolume;
            AdjustAllSEVolume(seVolume);
        }
        else
        {
            Debug.LogError("SaveLoadManagerが見つかりません。");
        }
    }

    /// <summary>
    /// 名前でSEを再生
    /// </summary>
    // --- UI系 ---
    public void PlayUISE(SE_UI se)
    {
        if (seNameTable_UI.TryGetValue(se, out var seName))
        {
            PlaySE(seName);
        }
        else
        {
            Debug.LogWarning($"SE name not found for UI SE: {se}");
        }
    }

    // --- PlayerAction系 ---
    public void PlayPlayerActionSE(SE_PlayerAction se)
    {
        if (seNameTable_PlayerAction.TryGetValue(se, out var seName))
        {
            PlaySE(seName);
        }
        else
        {
            Debug.LogWarning($"SE name not found for PlayerAction SE: {se}");
        }
    }

    // --- EnemyAction系 ---
    public void PlayEnemyActionSE(SE_EnemyAction se)
    {
        if (seNameTable_EnemyAction.TryGetValue(se, out var seName))
        {
            PlaySE(seName);
        }
        else
        {
            Debug.LogWarning($"SE name not found for EnemyAction SE: {se}");
        }
    }

    // --- Field系 ---
    public void PlayFieldSE(SE_Field se)
    {
        if (seNameTable_Field.TryGetValue(se, out var seName))
        {
            PlaySE(seName);
        }
        else
        {
            Debug.LogWarning($"SE name not found for Field SE: {se}");
        }
    }

    // --- SystemEvent系 ---
    public void PlaySystemEventSE(SE_SystemEvent se)
    {
        if (seNameTable_SystemEvent.TryGetValue(se, out var seName))
        {
            PlaySE(seName);
        }
        else
        {
            Debug.LogWarning($"SE name not found for SystemEvent SE: {se}");
        }
    }

    private void PlaySE(string seName)
    {
        if (seDictionary.TryGetValue(seName, out AudioSource source))
        {
            source.Play();
            if (originalPitchs.TryGetValue(seName, out float originalPitch))
            {
                source.pitch = originalPitch; //ピッチを初期化
            }
        }
        else
        {
            Debug.LogWarning("SE not found: " + seName);
        }
    }

    /// <summary>
    /// 名前でSEを停止
    /// </summary>
    // --- UI系 ---
    public void StopUISE(SE_UI se)
    {
        if (seNameTable_UI.TryGetValue(se, out var seName))
        {
            StopSE(seName);
        }
        else
        {
            Debug.LogWarning($"SE name not found for UI SE: {se}");
        }
    }

    // --- PlayerAction系 ---
    public void StopPlayerActionSE(SE_PlayerAction se)
    {
        if (seNameTable_PlayerAction.TryGetValue(se, out var seName))
        {
            StopSE(seName);
        }
        else
        {
            Debug.LogWarning($"SE name not found for PlayerAction SE: {se}");
        }
    }

    // --- EnemyAction系 ---
    public void StopEnemyActionSE(SE_EnemyAction se)
    {
        if (seNameTable_EnemyAction.TryGetValue(se, out var seName))
        {
            StopSE(seName);
        }
        else
        {
            Debug.LogWarning($"SE name not found for EnemyAction SE: {se}");
        }
    }

    // --- Field系 ---
    public void StopFieldSE(SE_Field se)
    {
        if (seNameTable_Field.TryGetValue(se, out var seName))
        {
            StopSE(seName);
        }
        else
        {
            Debug.LogWarning($"SE name not found for Field SE: {se}");
        }
    }

    // --- SystemEvent系 ---
    public void StopSystemEventSE(SE_SystemEvent se)
    {
        if (seNameTable_SystemEvent.TryGetValue(se, out var seName))
        {
            StopSE(seName);
        }
        else
        {
            Debug.LogWarning($"SE name not found for SystemEvent SE: {se}");
        }
    }

    private void StopSE(string seName)
    {
        if (seDictionary.TryGetValue(seName, out AudioSource source))
        {
            source.Stop();
        }
    }

    /// <summary>
    /// 全てのSEを停止
    /// </summary>
    public void StopAllSE()
    {
        foreach (var source in seDictionary.Values)
        {
            if (source.isPlaying)
            {
                source.Stop();
            }
        }
    }

    /// <summary>
    /// 全SEの音量を初期音量 × ratio に調整
    /// </summary>
    public void AdjustAllSEVolume(float ratio)
    {
        foreach (var kvp in seDictionary)
        {
            string name = kvp.Key;
            AudioSource source = kvp.Value;

            if (originalVolumes.TryGetValue(name, out float originalVolume))
            {
                source.volume = originalVolume * ratio;
            }
        }
    }

    /// <summary>
    /// 指定したSEが再生中かどうかを返す
    /// </summary>
    // --- UI系 ---
    public bool IsPlayingUISE(SE_UI se)
    {
        if (seNameTable_UI.TryGetValue(se, out var seName))
        {
            return IsPlaying(seName);
        }
        else
        {
            Debug.LogWarning($"SE name not found for UI SE: {se}");
            return false;
        }
    }

    // --- PlayerAction系 ---
    public bool IsPlayingPlayerActionSE(SE_PlayerAction se)
    {
        if (seNameTable_PlayerAction.TryGetValue(se, out var seName))
        {
            return IsPlaying(seName);
        }
        else
        {
            Debug.LogWarning($"SE name not found for PlayerAction SE: {se}");
            return false;
        }
    }

    // --- EnemyAction系 ---
    public bool IsPlayingEnemyActionSE(SE_EnemyAction se)
    {
        if (seNameTable_EnemyAction.TryGetValue(se, out var seName))
        {
            return IsPlaying(seName);
        }
        else
        {
            Debug.LogWarning($"SE name not found for EnemyAction SE: {se}");
            return false;
        }
    }

    // --- Field系 ---
    public bool IsPlayingFieldSE(SE_Field se)
    {
        if (seNameTable_Field.TryGetValue(se, out var seName))
        {
            return IsPlaying(seName);
        }
        else
        {
            Debug.LogWarning($"SE name not found for Field SE: {se}");
            return false;
        }
    }

    // --- SystemEvent系 ---
    public bool IsPlayingSystemEventSE(SE_SystemEvent se)
    {
        if (seNameTable_SystemEvent.TryGetValue(se, out var seName))
        {
            return IsPlaying(seName);
        }
        else
        {
            Debug.LogWarning($"SE name not found for SystemEvent SE: {se}");
            return false;
        }
    }

    private bool IsPlaying(string seName)
    {
        if (seDictionary.TryGetValue(seName, out AudioSource source))
        {
            return source.isPlaying;
        }
        return false;
    }

    /// <summary>
    /// 指定したSEをピッチを変更して再生するメソッド
    /// </summary>
    // --- UI系 ---
    public void PlayUISEPitch(SE_UI se, float pitch)
    {
        if (seNameTable_UI.TryGetValue(se, out var seName))
        {
            PlaySEPitch(seName, pitch);
        }
        else
        {
            Debug.LogWarning($"SE name not found for UI SE: {se}");
        }
    }

    // --- PlayerAction系 ---
    public void PlayPlayerActionSEPitch(SE_PlayerAction se, float pitch)
    {
        if (seNameTable_PlayerAction.TryGetValue(se, out var seName))
        {
            PlaySEPitch(seName, pitch);
        }
        else
        {
            Debug.LogWarning($"SE name not found for PlayerAction SE: {se}");
        }
    }

    // --- EnemyAction系 ---
    public void PlayEnemyActionSEPitch(SE_EnemyAction se, float pitch)
    {
        if (seNameTable_EnemyAction.TryGetValue(se, out var seName))
        {
            PlaySEPitch(seName, pitch);
        }
        else
        {
            Debug.LogWarning($"SE name not found for EnemyAction SE: {se}");
        }
    }

    // --- Field系 ---
    public void PlayFieldSEPitch(SE_Field se, float pitch)
    {
        if (seNameTable_Field.TryGetValue(se, out var seName))
        {
            PlaySEPitch(seName, pitch);
        }
        else
        {
            Debug.LogWarning($"SE name not found for Field SE: {se}");
        }
    }

    // --- SystemEvent系 ---
    public void PlaySystemEventSEPitch(SE_SystemEvent se, float pitch)
    {
        if (seNameTable_SystemEvent.TryGetValue(se, out var seName))
        {
            PlaySEPitch(seName, pitch);
        }
        else
        {
            Debug.LogWarning($"SE name not found for SystemEvent SE: {se}");
        }
    }

    public void PlaySEPitch(string seName, float pitch)
    {
        if (seDictionary.TryGetValue(seName, out AudioSource source))
        {
            source.pitch = pitch;
            source.Play(); // SEを再生
        }
        else
        {
            Debug.LogWarning("SE not found: " + seName);
        }
    }

    /// <summary>
    /// 指定したSEを長さを変更して再生するメソッド
    /// </summary>
    // --- UI系 ---
    public void PlayUISELength(SE_UI se, float length)
    {
        if (seNameTable_UI.TryGetValue(se, out var seName))
        {
            PlaySELength(seName, length);
        }
        else
        {
            Debug.LogWarning($"SE name not found for UI SE: {se}");
        }
    }

    // --- PlayerAction系 ---
    public void PlayPlayerActionSELength(SE_PlayerAction se, float length)
    {
        if (seNameTable_PlayerAction.TryGetValue(se, out var seName))
        {
            PlaySELength(seName, length);
        }
        else
        {
            Debug.LogWarning($"SE name not found for PlayerAction SE: {se}");
        }
    }

    // --- EnemyAction系 ---
    public void PlayEnemyActionSELength(SE_EnemyAction se, float length)
    {
        if (seNameTable_EnemyAction.TryGetValue(se, out var seName))
        {
            PlaySELength(seName, length);
        }
        else
        {
            Debug.LogWarning($"SE name not found for EnemyAction SE: {se}");
        }
    }

    // --- Field系 ---
    public void PlayFieldSELength(SE_Field se, float length)
    {
        if (seNameTable_Field.TryGetValue(se, out var seName))
        {
            PlaySELength(seName, length);
        }
        else
        {
            Debug.LogWarning($"SE name not found for Field SE: {se}");
        }
    }

    // --- SystemEvent系 ---
    public void PlaySystemEventSELength(SE_SystemEvent se, float length)
    {
        if (seNameTable_SystemEvent.TryGetValue(se, out var seName))
        {
            PlaySELength(seName, length);
        }
        else
        {
            Debug.LogWarning($"SE name not found for SystemEvent SE: {se}");
        }
    }

    private void PlaySELength(string seName, float lenght)
    {
        if (seDictionary.TryGetValue(seName, out AudioSource source))
        {
            float originalLength = originalLenght[seName];
            float newPitch = originalLength / lenght;
            source.pitch = newPitch;
            source.Play(); // SEを再生
        }
        else
        {
            Debug.LogWarning("SE not found: " + seName);
        }
    }

    /// <summary>
    /// 指定したSEをの長さを取得するメソッド
    /// </summary>
    // --- UI系 ---
    public float GetUISELength(SE_UI se)
    {
        if (seNameTable_UI.TryGetValue(se, out var seName))
        {
            return GetSELength(seName);
        }
        else
        {
            Debug.LogWarning($"SE name not found for UI SE: {se}");
            return 0f;
        }
    }

    // --- PlayerAction系 ---
    public float GetPlayerActionSELength(SE_PlayerAction se)
    {
        if (seNameTable_PlayerAction.TryGetValue(se, out var seName))
        {
            return GetSELength(seName);
        }
        else
        {
            Debug.LogWarning($"SE name not found for PlayerAction SE: {se}");
            return 0f;
        }
    }

    // --- EnemyAction系 ---
    public float GetEnemyActionSELength(SE_EnemyAction se)
    {
        if (seNameTable_EnemyAction.TryGetValue(se, out var seName))
        {
            return GetSELength(seName);
        }
        else
        {
            Debug.LogWarning($"SE name not found for EnemyAction SE: {se}");
            return 0f;
        }
    }

    // --- Field系 ---
    public float GetFieldSELength(SE_Field se)
    {
        if (seNameTable_Field.TryGetValue(se, out var seName))
        {
            return GetSELength(seName);
        }
        else
        {
            Debug.LogWarning($"SE name not found for Field SE: {se}");
            return 0f;
        }
    }

    // --- SystemEvent系 ---
    public float GetSystemEventSELength(SE_SystemEvent se)
    {
        if (seNameTable_SystemEvent.TryGetValue(se, out var seName))
        {
            return GetSELength(seName);
        }
        else
        {
            Debug.LogWarning($"SE name not found for SystemEvent SE: {se}");
            return 0f;
        }
    }

    private float GetSELength(string seName)
    {
        if (seDictionary.TryGetValue(seName, out AudioSource source))
        {
            float length = source.clip.length;
            return length;
        }

        Debug.LogWarning("SE not found: " + seName);
        // SEが見つからない場合は0を返すか、適切なエラーハンドリングを行う
        return 0f;
    }
}
