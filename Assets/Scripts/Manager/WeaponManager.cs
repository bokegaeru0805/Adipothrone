using System;
using System.Collections.Generic;
using System.Linq;
using Shapes2D;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WeaponManager : MonoBehaviour
{
    public static WeaponManager instance;

    [SerializeField, Header("武器データベース")]
    private WeaponItemDatabase weaponItemDatabase;
    public Dictionary<Enum, WeaponFullData> weaponLookupDic { get; private set; }
    public Dictionary<Enum, WeaponFullData> shootLookupDic { get; private set; }
    public Dictionary<Enum, WeaponFullData> bladeLookupDic { get; private set; }
    public List<WeaponSaveData> shootOwnedList { get; private set; }
    public List<WeaponSaveData> bladeOwnedList { get; private set; }

    public class WeaponFullData
    {
        public WeaponData weaponData;
        public WeaponSaveData saveData;

        public WeaponFullData(WeaponData weaponData, WeaponSaveData saveData)
        {
            this.weaponData = weaponData;
            this.saveData = saveData;
        }
    }

    public enum WeaponType
    {
        shoot = 1,
        blade = 2,
    }

    [Min(1)]
    private float bladeRangeLimit = 100; // ブレード武器の射程距離上限

    [Min(1)]
    private float bladeHandlingLimit = 100; // ブレード武器の取り回し上限

    [Min(1)]
    private float shootSpeedLimit = 100; // シュート武器の速度上限

    public event Action<Enum> OnWeaponReplaced; // 武器が置き換えられたときのイベント

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            //DontDestroyOnLoad(this.gameObject);

            if (weaponItemDatabase == null)
            {
                Debug.LogWarning("WeaponManagerにWeaponItemDatabaseが設定されていません");
            }
        }
        else
        {
            Destroy(this.gameObject);
        }
    }

    private void Start()
    {
        //武器追加時のイベントの登録
        GameManager.instance.savedata.WeaponInventoryData.OnWeaponAdded += RebuildOwnedWeaponData;
    }

    //セーブデータからの参照用辞書・リストの再構築
    public void RebuildOwnedWeaponData()
    {
        //セーブデータから所持武器情報を取得
        BuildWeaponDictionary();
        //セーブデータから所持武器リストを取得
        BuildWeaponList();
    }

    private void OnDestroy()
    {
        //イベントの登録解除
        if (GameManager.instance != null && GameManager.instance.savedata != null)
        {
            GameManager.instance.savedata.WeaponInventoryData.OnWeaponAdded -=
                RebuildOwnedWeaponData;
        }
    }

    /// <summary>
    /// セーブデータから所持している武器情報を取得し、
    /// 各武器IDに対応する <see cref="WeaponFullData"/> を生成して辞書に格納します。
    /// GameManager や SaveData が存在しない場合や、所持武器リストが空の場合は処理を中断します。
    /// </summary>
    private void BuildWeaponDictionary()
    {
        if (GameManager.instance == null || GameManager.instance.savedata == null)
        {
            Debug.LogWarning("GameManagerまたはSaveDataが存在しません");
            return;
        }

        //セーブデータから所持武器データを取得
        var allSaveDataList = GameManager.instance.savedata.WeaponInventoryData.ownedWeapons;
        if (allSaveDataList == null || allSaveDataList.Count == 0)
        {
            Debug.LogWarning("WeaponInventoryDataがnullまたは空です");
            return;
        }

        //辞書を初期化
        weaponLookupDic = new Dictionary<Enum, WeaponFullData>();
        shootLookupDic = new Dictionary<Enum, WeaponFullData>();
        bladeLookupDic = new Dictionary<Enum, WeaponFullData>();

        foreach (var save in allSaveDataList)
        {
            // IDをEnumに変換
            Enum idEnum = EnumIDUtility.FromID(save.WeaponID);

            // 該当するWeaponDataを探す
            WeaponData weaponData = GetWeaponByID(idEnum);

            if (weaponData != null)
            {
                // 武器データが見つかった場合、WeaponFullDataを作成して辞書に追加
                WeaponFullData fullData = new WeaponFullData(weaponData, save);
                weaponLookupDic[idEnum] = fullData;

                // 専用辞書にも追加
                if (weaponData is ShootWeaponData shoot)
                {
                    shootLookupDic[shoot.weaponID] = fullData;
                }
                else if (weaponData is BladeWeaponData blade)
                {
                    bladeLookupDic[blade.weaponID] = fullData;
                }
            }
            else
            {
                Debug.LogWarning($"WeaponData not found for ID: {idEnum}");
            }
        }
    }

    /// <summary>
    /// 所持している武器の全てのデータをIDから取得します。
    /// </summary>
    public WeaponFullData GetOwnedWeaponData(Enum id)
    {
        return weaponLookupDic.TryGetValue(id, out var data) ? data : null;
    }

    private void BuildWeaponList()
    {
        if (GameManager.instance == null || GameManager.instance.savedata == null)
        {
            Debug.LogWarning("GameManagerまたはSaveDataが存在しません");
            return;
        }

        //セーブデータから所持武器データを取得
        var allSaveDataList = GameManager.instance.savedata.WeaponInventoryData.ownedWeapons;
        if (allSaveDataList == null || allSaveDataList.Count == 0)
        {
            Debug.LogWarning("WeaponInventoryDataがnullまたは空です");
            return;
        }

        //辞書を初期化
        shootOwnedList = new List<WeaponSaveData>();
        bladeOwnedList = new List<WeaponSaveData>();

        //武器の種類ごとに分ける
        //所持武器のリストを取得
        foreach (var save in allSaveDataList)
        {
            // IDをEnumに変換
            Enum idEnum = EnumIDUtility.FromID(save.WeaponID);

            if (idEnum is ShootName)
            {
                shootOwnedList.Add(save);
            }
            else if (idEnum is BladeName)
            {
                bladeOwnedList.Add(save);
            }
        }
    }

    //装備中の武器を入れ替える関数
    public void ReplaceEquippedWeapon(Enum weaponID)
    {
        if (weaponID is not BladeName bladeName && weaponID is not ShootName shootName)
        {
            Debug.LogWarning($"{weaponID}はBladeNameまたはShootNameではありません");
            return;
        }

        //数字IDに変換
        int IDNumber = EnumIDUtility.ToID(weaponID);
        //IDからタイプIDを取得
        int typeIDNumber = EnumIDUtility.ExtractTypeID(IDNumber);

        //セーブデータから所持武器データを取得
        var inventory = GameManager.instance.savedata.WeaponInventoryData.ownedWeapons;
        //セーブデータから装備武器データを取得
        var equipment = GameManager.instance.savedata.WeaponEquipmentData.ownedWeapons;

        if (inventory == null || equipment == null)
        {
            Debug.LogWarning("WeaponInventoryDataかWeaponEquipmentDataが存在しません");
            return;
        }

        // equippedWeaponsから、同じカテゴリの武器をすべて削除
        equipment.RemoveAll(w => EnumIDUtility.ExtractTypeID(w.WeaponID) == typeIDNumber);

        // ownedWeaponsから、指定したweaponIDのデータを探す
        var weaponToEquip = inventory.Find(w => w.WeaponID == IDNumber);
        if (weaponToEquip == null)
        {
            Debug.LogWarning($"指定されたWeaponID {weaponID} は所持していません");
            return;
        }

        // 参照追加
        equipment.Add(weaponToEquip);
        // 装備変更イベントを発火
        OnWeaponReplaced?.Invoke(weaponID);
    }

    /// <summary>
    /// 装備中の全武器のIDを取得し、同じIDの所持武器(inventory)の参照に置き換える
    /// </summary>
    public void ReplaceAllEquippedWeaponsWithInventoryReferences()
    {
        var inventory = GameManager.instance.savedata.WeaponInventoryData?.ownedWeapons;
        var equipment = GameManager.instance.savedata.WeaponEquipmentData?.ownedWeapons;

        if (inventory == null || equipment == null)
        {
            Debug.LogWarning("WeaponInventoryDataまたはWeaponEquipmentDataがnullです");
            return;
        }

        // 装備中の全てのWeaponIDをリスト化
        List<int> equippedIDs = equipment.Select(w => w.WeaponID).ToList();

        // 装備をすべて削除（参照を切る）
        equipment.Clear();

        // 対応するインベントリの参照を装備に追加
        foreach (int weaponID in equippedIDs)
        {
            var inventoryWeapon = inventory.Find(w => w.WeaponID == weaponID);
            if (inventoryWeapon != null)
            {
                equipment.Add(inventoryWeapon);
            }
            else
            {
                Debug.LogWarning($"InventoryにWeaponID {weaponID} が見つかりませんでした");
            }
        }
    }

    /// <summary>
    /// 選択された武器の詳細を表示します。
    /// </summary>
    public void DisplaySelectedWeaponDetails(
        Enum weaponID,
        TextMeshProUGUI AttackPowerText,
        TextMeshProUGUI WpCostText,
        GameObject RangeOrSpeedBar,
        Image RangeOrSpeedBarImage,
        GameObject HandlingBar,
        Image HandlingBarImage,
        TextMeshProUGUI PenetrationText
    )
    {
        //武器UIの初期化
        AttackPowerText.text = "";
        WpCostText.text = "";
        RangeOrSpeedBar.SetActive(false);
        HandlingBar.SetActive(false);
        PenetrationText.text = "";

        if (weaponID == null)
        {
            return;
        }

        //武器データを取得
        var weaponData = GetWeaponByID(weaponID);

        // 武器データが見つからない場合は何もしない
        if (weaponData == null)
        {
            return;
        }

        // 武器の射程距離または速度を表示
        if (weaponData is ShootWeaponData shootWeapon)
        {
            //弾の攻撃力の数値を表示
            AttackPowerText.text = shootWeapon.power.ToString();
            //弾のWP消費量の数値を表示
            WpCostText.text = shootWeapon.wpCost.ToString();
            //弾の速度のバーを表示
            RangeOrSpeedBar.SetActive(true);
            //弾の速度のバーの表示を設定
            RangeOrSpeedBarImage.fillAmount = shootWeapon.shootSpeed / shootSpeedLimit;
            //弾の貫通数の数値を表示
            PenetrationText.text = shootWeapon.penetrationLimitCount.ToString();
        }
        else if (weaponData is BladeWeaponData bladeWeapon)
        {
            //剣の攻撃力の数値を表示
            AttackPowerText.text = bladeWeapon.power.ToString();
            //剣のWP消費量の数値を表示
            WpCostText.text = bladeWeapon.wpCost.ToString();
            //剣のレンジのバーを表示
            RangeOrSpeedBar.SetActive(true);
            //剣のレンジのバーの表示を設定
            RangeOrSpeedBarImage.fillAmount = bladeWeapon.ColliderSize.magnitude / bladeRangeLimit;
            //剣の取り回しのバーを表示
            HandlingBar.SetActive(true);
            //剣の取り回しのバーの表示を設定
            HandlingBarImage.fillAmount = bladeWeapon.attackTime / bladeHandlingLimit;
        }
    }

    public ShootWeaponData GetShootByID(Enum id)
    {
        return weaponItemDatabase.GetShootByID(id);
    }

    public BladeWeaponData GetBladeByID(Enum id)
    {
        return weaponItemDatabase.GetBladeByID(id);
    }

    public WeaponData GetWeaponByID(Enum id)
    {
        return weaponItemDatabase.GetWeaponData(id);
    }

    // // IDからBase武器データを取得
    // public WeaponData GetBaseWeaponDataByID(int id)
    // {
    //     return weaponItemDatabase.GetWeaponByID(id);
    // }

    // // IDからタイプごとの武器データを取得
    // public T GetWeaponDataByID<T>(int id)
    //     where T : WeaponData
    // {
    //     return weaponItemDatabase.GetWeaponByID<T>(id);
    // }
}
