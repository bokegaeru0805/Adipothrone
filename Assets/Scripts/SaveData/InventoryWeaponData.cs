using System;
using System.Collections.Generic;
using System.Linq;

[Serializable]
public class WeaponSaveData
{
    public int WeaponID; // 固定情報への参照用
    public Enum EnumWeaponID => EnumIDUtility.FromID(WeaponID); // Enumへの変換用プロパティ
    public int Stock; //所持数
    public int AttackCount; //攻撃した回数
    public int WeaponLevel; //武器のレベル
    public int WeaponExp; //武器の経験値

    public WeaponSaveData(int weaponID)
    {
        WeaponID = weaponID;
        AttackCount = 0;
        Stock = 1;
        WeaponLevel = 1;
        WeaponExp = 0;
    }
}

[System.Serializable]
public class InventoryWeaponData
{
    public List<WeaponSaveData> ownedWeapons = new List<WeaponSaveData>();

    public enum WeaponType
    {
        None = 0, // 武器なし
        shoot = 1,
        wave = 2,
        blade = 3,
    }

    private readonly Dictionary<WeaponType, int> weaponTypeDigits =
        new() { { WeaponType.shoot, (int)TypeID.Shoot }, { WeaponType.blade, (int)TypeID.Blade } };

    public event Action OnWeaponAdded; // 武器追加時のイベント

    // 武器を追加
    public void AddWeapon(Enum weaponID, int amount = 1)
    {
        int weaponIDNumber = EnumIDUtility.ToID(weaponID);
        var weapon = ownedWeapons.Find(w => w.WeaponID == weaponIDNumber);
        if (weapon != null)
        {
            weapon.Stock += amount;
        }
        else
        {
            WeaponSaveData newWeapon = new WeaponSaveData(weaponIDNumber);
            newWeapon.Stock = amount;
            ownedWeapons.Add(newWeapon);
        }

        OnWeaponAdded?.Invoke(); // 武器追加時のイベントを発火
    }

    // 武器を使用（在庫を減らす、戻り値: 成功/失敗）
    public bool UseWeapon(Enum weaponID, int amount = 1)
    {
        int weaponIDNumber = EnumIDUtility.ToID(weaponID);
        var weapon = ownedWeapons.Find(w => w.WeaponID == weaponIDNumber);
        if (weapon != null && weapon.Stock >= amount)
        {
            weapon.Stock -= amount;
            if (weapon.Stock <= 0)
            {
                ownedWeapons.Remove(weapon);
            }
            return true;
        }
        return false;
    }

    //武器を所持しているかどうかを取得
    public bool HasWeapon(Enum weaponID)
    {
        int weaponIDNumber = EnumIDUtility.ToID(weaponID);
        var weapon = ownedWeapons.Find(w => w.WeaponID == weaponIDNumber);
        if (weapon != null && weapon.Stock > 0)
        {
            return true;
        }
        else
        {
            return false;
        }
    }

    // 武器の所持数を取得
    public int GetWeaponAmount(Enum weaponID)
    {
        int weaponIDNumber = EnumIDUtility.ToID(weaponID);
        var weapon = ownedWeapons.Find(w => w.WeaponID == weaponIDNumber);
        return weapon?.Stock ?? 0;
    }

    // 指定した種類の武器を全て取得（List形式）
    public List<WeaponSaveData> GetAllWeaponsByType(WeaponType weaponType)
    {
        int typeDigit = weaponTypeDigits[weaponType];
        return ownedWeapons
            .Where(w => EnumIDUtility.ExtractTypeID(w.WeaponID) == typeDigit)
            .ToList();
    }

    /// <summary>
    /// 指定した種類の武器の中から、最初に見つかったものを1つだけ返します。
    /// </summary>
    /// <param name="weaponType">検索する武器の種類</param>
    /// <returns>最初に見つかったWeaponSaveData。見つからない場合はnullを返します。</returns>
    public WeaponSaveData GetFirstWeaponByType(WeaponType weaponType)
    {
        // 武器の種類に対応するIDの桁番号を取得
        int typeDigit = weaponTypeDigits[weaponType];

        // ownedWeaponsリストの中から、指定された種類に一致する最初の要素を返す
        // 一致する要素がなければnullを返す
        return ownedWeapons.FirstOrDefault(w =>
            EnumIDUtility.ExtractTypeID(w.WeaponID) == typeDigit
        );
    }

    /// <summary>
    /// 指定した種類（shoot, bladeなど）の武器を一つでも所持しているかを確認します。
    /// </summary>
    /// <param name="weaponType">確認したい武器の種類</param>
    /// <returns>その種類の武器を一つでも所持していればtrue、一つもなければfalseを返します。</returns>
    public bool HasAnyWeaponOfType(WeaponType weaponType)
    {
        // 武器の種類に対応するIDの桁番号を取得します
        int typeDigit = weaponTypeDigits[weaponType];

        // ownedWeaponsリストの中に、武器IDのタイプが一致するものが「一つでも存在するか」をチェックします。
        // Any() は条件に合うものが最初に見つかった瞬間に true を返して処理を終えるため、非常に効率的です。
        return ownedWeapons.Any(w => EnumIDUtility.ExtractTypeID(w.WeaponID) == typeDigit);
    }

    /// <summary>
    /// 指定されたアイテムタイプ（例: 武器）の所持データを ItemEntry のリストとして取得する。
    /// </summary>
    /// <param name="type">取得したいアイテムタイプ（例: ItemType.Weapon）</param>
    /// <returns>ItemEntry のリスト（itemID と count）</returns>
    public List<ItemEntry> GetAllItemByType(WeaponType type)
    {
        // タイプに対応する桁番号を取得
        int typeDigit = weaponTypeDigits[type];

        // 所持武器の中から、指定タイプのものだけを抽出して ItemEntry に変換する
        return ownedWeapons
            .Where(w => EnumIDUtility.ExtractTypeID(w.WeaponID) == typeDigit) // 指定タイプに一致するか判定
            .Select(w => new ItemEntry(w.WeaponID, w.Stock)) // WeaponSaveData → ItemEntry に変換
            .ToList(); // リストに変換して返す
    }

    // ID順にソート
    public void SortWeaponsByID()
    {
        ownedWeapons = ownedWeapons.OrderBy(w => w.WeaponID).ToList();
    }
}
