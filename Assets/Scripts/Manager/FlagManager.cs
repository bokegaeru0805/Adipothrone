using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Enumに初期値を指定するためのカスタム属性。
/// </summary>
[AttributeUsage(AttributeTargets.Field)]
public class InitialValueAttribute : Attribute
{
    public int Value { get; }

    public InitialValueAttribute(int value)
    {
        Value = value;
    }
}

/// <summary>
/// フラグ（ゲーム状態）を一元管理するクラス。
/// Bool/Int/鍵の開閉などをEnumベースで保存・読み込み可能。
/// </summary>
public class FlagManager : MonoBehaviour
{
    public static FlagManager instance { get; private set; }

    /// <summary>
    /// bool型のフラグが変更されたときに発行されるイベント。
    /// 引数: (変更されたフラグのEnum, 新しい値)
    /// </summary>
    public static event Action<Enum, bool> OnBoolFlagChanged;

    /// <summary>
    /// int型のフラグが変更されたときに発行されるイベント。
    /// 引数: (変更されたフラグのEnum, 新しい値)
    /// </summary>
    public static event Action<Enum, int> OnIntFlagChanged;

    /// <summary>
    /// KeyIDのフラグが変更されたときに発行されるイベント。
    /// 引数: (変更されたKeyID, 新しい値)
    /// </summary>
    public static event Action<KeyID, bool> OnKeyFlagChanged;

    // 各種フラグの保存領域（Enumをキーにする）
    private Dictionary<Enum, bool> boolFlags = new();
    private Dictionary<Enum, int> intFlags = new();
    private Dictionary<KeyID, bool> keyOpenStatus = new();

    /// <summary>
    /// セーブ用構造体。Enumはintに変換して保存する。
    /// </summary>
    [Serializable]
    public class FlagSaveData
    {
        public Dictionary<int, bool> boolFlags = new();
        public Dictionary<int, int> intFlags = new();
        public Dictionary<int, bool> keyOpenStatus = new();
    }

    /// <summary>
    /// 大ドアの解放条件（必要なKeyIDのリスト）
    /// </summary>
    [Serializable]
    public class DoorUnlockCondition
    {
        public int doorId;
        public List<KeyID> requiredKeys;
    }

    // Bool型の初期値（必要に応じて定義）
    private readonly Dictionary<Enum, bool> defaultBoolValues = new();

    // 大ドアの条件設定（インスペクター上で編集可能）
    [SerializeField]
    private List<DoorUnlockCondition> doorConditions =
        new()
        {
            new DoorUnlockCondition
            {
                doorId = 1,
                requiredKeys = new List<KeyID> { KeyID.K1_1 },
            },
            new DoorUnlockCondition
            {
                doorId = 2,
                requiredKeys = new List<KeyID> { KeyID.K2_1, KeyID.K2_2, KeyID.K2_3 },
            },
            // new DoorUnlockCondition
            // {
            //     doorId = 3,
            //     requiredKeys = new List<KeyID> { KeyID.K3_1, KeyID.K3_2, KeyID.K3_3 },
            // },
            new DoorUnlockCondition
            {
                doorId = 4,
                requiredKeys = new List<KeyID> { KeyID.K4_1, KeyID.K4_2, KeyID.K4_3 },
            },
        };

    /// <summary>
    /// シングルトン初期化。重複インスタンスは破棄。
    /// </summary>
    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            //DontDestroyOnLoad(gameObject);
            ResetAllFlags();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // Bool型フラグの設定と取得
    public void SetBoolFlag<T>(T flag, bool value)
        where T : Enum
    {
        if (GetBoolFlag(flag) != value)
        {
            boolFlags[flag] = value;
            OnBoolFlagChanged?.Invoke(flag, value);
        }
    }

    public bool GetBoolFlag<T>(T flag)
        where T : Enum => boolFlags.TryGetValue(flag, out var val) && val;

    // Int型フラグの設定と取得
    public void SetIntFlag<T>(T flag, int value)
        where T : Enum
    {
        if (GetIntFlag(flag) != value)
        {
            intFlags[flag] = value;
            OnIntFlagChanged?.Invoke(flag, value);
        }
    }

    public int GetIntFlag<T>(T flag)
        where T : Enum => intFlags.TryGetValue(flag, out var val) ? val : 0;

    public void IncrementIntFlag<T>(T flag, int amount = 1)
        where T : Enum
    {
        var newValue = GetIntFlag(flag) + amount;
        intFlags[flag] = newValue;
        OnIntFlagChanged?.Invoke(flag, newValue);
    }

    // 鍵の開閉状態の操作
    public void SetKeyOpened(KeyID keyId, bool isOpen)
    {
        if (GetKeyOpened(keyId) != isOpen)
        {
            keyOpenStatus[keyId] = isOpen;
            OnKeyFlagChanged?.Invoke(keyId, isOpen);
        }
    }

    public bool GetKeyOpened(KeyID keyId) =>
        keyOpenStatus.TryGetValue(keyId, out var result) && result;

    /// <summary>
    /// 大ドアの開放条件を満たしているか確認
    /// </summary>
    public bool IsDoorUnlocked(int doorId)
    {
        var condition = doorConditions.Find(c => c.doorId == doorId);
        if (condition == null)
        {
            Debug.LogWarning($"ドアID {doorId} に対応する開放条件がありません。");
            return false;
        }

        foreach (var key in condition.requiredKeys)
        {
            if (!GetKeyOpened(key))
                return false;
        }
        return true;
    }

    /// <summary>
    /// セーブデータに変換（Enum→int）
    /// </summary>
    public FlagSaveData SaveFlagData()
    {
        // セーブ用の空データオブジェクトを作成する。
        // 現在のフラグ状態（bool/int/key）をこのオブジェクトに詰めて保存処理を行う。
        var data = new FlagSaveData();
        foreach (var kvp in boolFlags)
            data.boolFlags[Convert.ToInt32(kvp.Key)] = kvp.Value;

        foreach (var kvp in intFlags)
            data.intFlags[Convert.ToInt32(kvp.Key)] = kvp.Value;

        foreach (var kvp in keyOpenStatus)
            data.keyOpenStatus[(int)kvp.Key] = kvp.Value;

        return data;
    }

    /// <summary>
    /// セーブデータを読み込む
    /// </summary>
    public void LoadFlagData(FlagSaveData data)
    {
        boolFlags.Clear();
        intFlags.Clear();
        keyOpenStatus.Clear();

        // フラグごとにEnum型を指定して読み込む
        //チュートリアルステージのフラグ読み込み
        LoadBoolFlags<PrologueTriggeredEvent>(data.boolFlags);
        LoadIntFlags<PrologueCountedEvent>(data.intFlags);
        // 第一章ステージのフラグ読み込み
        LoadBoolFlags<Chapter1TriggeredEvent>(data.boolFlags);
        LoadIntFlags<Chapter1CountedEvent>(data.intFlags);
        LoadBoolFlags<TutorialEvent>(data.boolFlags);

        foreach (var kvp in data.keyOpenStatus)
        {
            KeyID key = (KeyID)kvp.Key;
            keyOpenStatus[key] = kvp.Value;
        }
    }

    /// <summary>
    /// 保存された bool 型のフラグ群（intキー）を、指定された Enum 型 T のキーとして変換し、
    /// 現在の boolFlags に読み込む。
    ///
    /// ※注意：
    /// ・Enum は型ごとに識別され、異なる Enum 間で同一の数値を持つケースもあるため、
    /// 　 int → Enum の変換は安全性が必要。
    /// ・この関数では Enum.IsDefined を用いて、指定 Enum 型 T に属するキーのみを取り扱い、
    /// 　 異なる Enum 型に属する値の誤読込みを防止している。
    ///
    /// ・もし今後 Enum 値の重複を許容しない方針で統一された場合でも、
    /// 　 型ごとの安全性確保のため、このチェック処理は維持すべき。
    /// </summary>
    /// <typeparam name="T">読み込む対象の Enum 型（例：PrologueTriggeredEvent, TutorialEvent など）</typeparam>
    /// <param name="savedFlags">int（Enum値）と bool 値のセーブデータ</param>
    private void LoadBoolFlags<T>(Dictionary<int, bool> savedFlags)
        where T : Enum
    {
        foreach (var kvp in savedFlags)
        {
            // 指定された Enum 型 T に定義されている値かどうかを確認
            if (Enum.IsDefined(typeof(T), kvp.Key))
            {
                // int → Enum へ安全にキャスト
                T key = (T)Enum.ToObject(typeof(T), kvp.Key);

                // 対応するフラグを boolFlags に登録
                boolFlags[key] = kvp.Value;
            }
        }
    }

    /// <summary>
    /// セーブデータから指定された Enum 型 T に属する int フラグを読み込む。
    ///
    /// ・Enum.IsDefined によって T 型に定義されていない値は除外され、
    /// 　異なる Enum 型からの誤読込みを防止。
    ///
    /// ・SaveFlagData → LoadFlagData の復元時に、型ごとのフラグを
    /// 　正確に復元するために使用される。
    /// </summary>
    private void LoadIntFlags<T>(Dictionary<int, int> savedFlags)
        where T : Enum
    {
        foreach (var kvp in savedFlags)
        {
            if (Enum.IsDefined(typeof(T), kvp.Key))
            {
                T key = (T)Enum.ToObject(typeof(T), kvp.Key);
                intFlags[key] = kvp.Value;
            }
        }
    }

    /// <summary>
    /// Boolフラグの初期化（未設定のものはfalseまたは定義された初期値）
    /// </summary>
    public void InitializeBoolEnum<T>()
        where T : Enum
    {
        foreach (T value in Enum.GetValues(typeof(T)))
        {
            if (!boolFlags.ContainsKey(value))
                boolFlags[value] = defaultBoolValues.TryGetValue(value, out var val) ? val : false;
        }
    }

    /// <summary>
    /// Intフラグの初期化（属性で初期値指定が可能）
    /// </summary>
    public void InitializeIntEnum<T>()
        where T : Enum
    {
        foreach (T value in Enum.GetValues(typeof(T)))
        {
            if (!intFlags.ContainsKey(value))
            {
                var member = typeof(T).GetMember(value.ToString())[0];
                var attr =
                    Attribute.GetCustomAttribute(member, typeof(InitialValueAttribute))
                    as InitialValueAttribute;
                intFlags[value] = attr?.Value ?? 0;
            }
        }
    }

    /// <summary>
    /// 全てのKeyIDに対して開閉状態をfalseで初期化
    /// </summary>
    private void InitializeKeyOpenStatus()
    {
        foreach (KeyID key in Enum.GetValues(typeof(KeyID)))
        {
            if (!keyOpenStatus.ContainsKey(key))
                keyOpenStatus[key] = false;
        }
    }

    /// <summary>
    /// ゲーム開始時にすべてのフラグを初期化
    /// </summary>
    public void InitializeAllEnums()
    {
        //プロローグのフラグ初期化
        InitializeBoolEnum<PrologueTriggeredEvent>();
        InitializeIntEnum<PrologueCountedEvent>();
        // 第一章のフラグの初期化
        InitializeBoolEnum<Chapter1TriggeredEvent>();
        InitializeIntEnum<Chapter1CountedEvent>();

        InitializeKeyOpenStatus();
    }

    /// <summary>
    /// すべてのフラグをリセットし初期状態に戻す
    /// </summary>
    public void ResetAllFlags()
    {
        boolFlags.Clear();
        intFlags.Clear();
        InitializeAllEnums();
    }

    #region ### UnityEvent用ラッパーメソッド ###

    // --- Bool型フラグ用ラッパー ---
    public void SetPrologueTriggeredEvent(PrologueTriggeredEvent flag, bool value)
    {
        // ジェネリックメソッドであるSetBoolFlagを呼び出す
        SetBoolFlag(flag, value);
    }

    public void SetChapter1TriggeredEvent(Chapter1TriggeredEvent flag, bool value)
    {
        SetBoolFlag(flag, value);
    }

    public void SetTutorialEvent(TutorialEvent flag, bool value)
    {
        SetBoolFlag(flag, value);
    }

    // --- Int型フラグ用ラッパー ---

    public void SetPrologueCountedEvent(PrologueCountedEvent flag, int value)
    {
        SetIntFlag(flag, value);
    }

    public void SetChapter1CountedEvent(Chapter1CountedEvent flag, int value)
    {
        SetIntFlag(flag, value);
    }

    // 他にUnityEventから設定したいEnumがあれば、同様にラッパーメソッドを追加してください。

    #endregion
}
