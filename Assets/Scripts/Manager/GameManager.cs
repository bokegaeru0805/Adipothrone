using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using Fungus;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    #region Singleton & Events

    public static GameManager instance { get; private set; } //シングルトン用のインスタンス

    public event Action OnAnyItemAddedToInventory; // 任意のアイテムがインベントリに追加されたときのイベント
    public event Action<Enum> OnAnyItemRemovedFromInventory; // 任意のアイテムがインベントリから削除されたときのイベント
    public static event Action<bool> OnTalkingStateChanged; // 会話状態が変化したときのイベント
    public event Action OnInventoryUpdated; // インベントリ（所持品）が更新されたときの共通イベント

    /// <summary>
    /// アイテムの増減やロードによってインベントリが更新されたことを全体に通知します。
    /// </summary>
    public void NotifyInventoryUpdated()
    {
        OnInventoryUpdated?.Invoke();
    }

    public event Action OnWeaponInventoryUpdated; // 武器データ（追加・変化）が更新されたときの共通イベント

    /// <summary>
    /// 武器の追加や変化、ロードによって武器データが更新されたことを全体に通知します。
    /// </summary>
    public void NotifyWeaponInventoryUpdated()
    {
        OnWeaponInventoryUpdated?.Invoke();
    }
    #endregion

    #region Static Flags & States

    public static bool isFirstGameOpen = false; //初めてゲームが起動されたか
    public static bool isFirstGameSceneOpen = false; //初めてゲームシーンが開かれたか
    public static bool IsJumpCooldownActive { get; private set; } = false; // 会話終了直後、ジャンプ入力を受け付けないクールダウン中かどうか
    #endregion

    #region Global References

    [Header("ゲーム全体で使用するデータベース")]
    [Tooltip("武器アイテムのデータベース")]
    public HealItemDatabase healItemDatabase;

    [Tooltip("ステータス強化アイテムのデータベース")]
    public StatusEnhanceItemDatabase statusEnhanceItemDatabase;

    [Tooltip("重要アイテムのデータベース")]
    public KeyItemDatabase keyItemDatabase;

    [Tooltip("レシピアイテムのデータベース")]
    public RecipeItemDatabase recipeItemDatabase;

    [Tooltip("Tips情報のデータベース")]
    public TipsInfoDatabase tipsInfoDatabase;

    [Header("ゲーム全体で使用するプレハブ")]
    [Tooltip("アイテムドロップのプレハブ")]
    public GameObject DropItemPrefab;

    [HideInInspector]
    public Flowchart globalFlowchart; //ゲーム全体のflowchartの参照

    [HideInInspector]
    public SaveData savedata = new SaveData(); //セーブデータを保存する変数

    private ItemDataManager itemDataManager; //アイテムデータベースマネージャーの参照
    private Block TreasureBlock; //宝箱開封時の会話のブロック
    #endregion

    #region Internal States
    public bool IsTalking => isTalking;
    private bool isTalking = false;
    private float jumpCooldownDuration = 0.2f; // ジャンプ入力を受け付けないクールダウン時間（秒）
    public Vector2? crossScenePlayerSpawnPoint = null; //シーン遷移後の次のプレイヤーのスポーン位置
    private Dictionary<int, int> tipsSortOrderMap; //Tipsの正しい並び順を高速に検索するための辞書（キャッシュ）
    #endregion

    #region Unity Lifecycle Methods

    /// <summary>
    /// シングルトンの初期化および各コンポーネント・データベースの参照設定を行います。
    /// </summary>
    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            //DontDestroyOnLoad(gameObject); //親オブジェクトがシーンが変わっても廃棄されないので不要
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        if (globalFlowchart == null)
        {
            foreach (var fc in FindObjectsOfType<Fungus.Flowchart>())
            {
                if (fc.name == "GlobalFlowchart") //GameObject名で比較
                {
                    globalFlowchart = fc;
                    break;
                }
            }

            if (globalFlowchart == null)
            {
                Debug.LogWarning("GlobalFlowChart が Scene 上に存在しません！");
            }
            else
            {
                TreasureBlock = globalFlowchart.FindBlock("Treasurebox");
                if (TreasureBlock == null)
                {
                    Debug.LogWarning("TreasureBlock が GlobalFlowchart 上に存在しません！");
                    return;
                }
            }
        }

        if (DropItemPrefab == null)
        {
            Debug.LogError("GameManagerにDropItemPrefabが設定されていません");
        }

        if (
            healItemDatabase == null
            || keyItemDatabase == null
            || tipsInfoDatabase == null
            || statusEnhanceItemDatabase == null
            || recipeItemDatabase == null
        )
        {
            Debug.LogError("GameManagerに必要なデータベースが設定されていません");
            return;
        }

        isFirstGameOpen = false; // 初回ゲームオープンフラグを初期化
        isFirstGameSceneOpen = false; // 初回ゲームシーンオープンフラグを初期化
        EndTalk(); // 会話中フラグをfalseで初期化

        //ゲーム開始時に、データベースから正しい並び順を一度だけ生成してキャッシュする
        InitializeTipsSortOrderMap();
    }

    private void Start()
    {
        itemDataManager = ItemDataManager.instance;
        if (itemDataManager == null)
        {
            Debug.LogError("GameManagerがItemDataManagerを見つけられません");
            return;
        }
    }

    private void Update()
    {
        if (Camera.main != null && DOTween.IsTweening(Camera.main, true))
        {
            //Cameraが存在して、かつそのTweenが動いているとき
            if (InputManager.instance.SkipDialogHold())
            {
                //スキップボタンが押されているとき
                Camera.main.DOComplete(); //カメラのTweenを完了させる
            }
        }
    }

    #endregion

    #region Game State & Scene Management

    /// <summary>
    /// シーン遷移後にプレイヤーを出現させる座標を一時的に保存します。
    /// この座標は次にロードされるシーンでのみ使用され、適用後は自動でクリアされます。
    /// </summary>
    /// <param name="pos">遷移先シーンでプレイヤーを出現させるワールド座標</param>
    public void SetCrossSceneSpawnPoint(Vector2 pos)
    {
        crossScenePlayerSpawnPoint = pos;
    }

    /// <summary>
    /// ゲームの状態を初期状態に戻します。
    /// 主にセーブデータをリセットし、セーブ機能を無効化するために使用します。
    /// </summary>
    public void ResetState()
    {
        savedata = new SaveData(); //セーブデータを初期化する
        if (SaveLoadManager.instance != null)
        {
            SaveLoadManager.instance.DisableSave(); //セーブを無効にする
        }
    }

    #endregion

    #region Dialog & Action Control

    /// <summary>
    /// 会話状態を開始します。
    /// </summary>
    public void StartTalk()
    {
        // 既に会話中なら何もしない
        if (isTalking)
            return;

        isTalking = true;
        // イベントを発行して、会話が始まったことを他のスクリプトに通知する
        OnTalkingStateChanged?.Invoke(true);
    }

    /// <summary>
    /// 会話状態を終了します。
    /// </summary>
    public void EndTalk()
    {
        // 会話中でなければ何もしない
        if (!isTalking)
            return;

        isTalking = false;
        // イベントを発行して、会話が終わったことを他のスクリプトに通知する
        OnTalkingStateChanged?.Invoke(false);

        // 会話終了後に入力クールダウンを開始する
        TriggerJumpCooldown();
    }

    /// <summary>
    /// 会話終了ボタンが押され続けている間待機し、ボタンが離された後に会話を終了します。
    /// これにより、会話終了直後のキー入力暴発を防ぎます。
    /// </summary>
    public IEnumerator DialogEnd()
    {
        if (InputManager.instance.SkipDialogHold())
        {
            yield return new WaitUntil(() => InputManager.instance.SkipDialogHold());
        }

        // 会話が終了したら、会話中フラグをOFFにする
        EndTalk();
    }

    /// <summary>
    /// 外部からジャンプのクールダウンを開始するための公開メソッド
    /// </summary>
    public void TriggerJumpCooldown()
    {
        // 既存のコルーチンが動いている可能性を考慮し、一度停止してから新しく開始する
        StopCoroutine(JumpCooldownCoroutine());
        StartCoroutine(JumpCooldownCoroutine());
    }

    /// <summary>
    /// 会話終了後のジャンプ入力クールダウンを処理するコルーチン
    /// </summary>
    private IEnumerator JumpCooldownCoroutine()
    {
        IsJumpCooldownActive = true;
        yield return new WaitForSeconds(jumpCooldownDuration);
        IsJumpCooldownActive = false;
    }

    /// <summary>
    /// 宝箱を開けた際のFungus会話ブロックを起動します。
    /// グローバルFlowchart内の "Treasurebox" ブロックを探し、
    /// 取得したアイテムの情報を設定してから実行します。
    /// 実際のアイテム取得処理は、Fungus内のAddItemCommandで行われます。
    /// </summary>
    /// <param name="itemData">取得したアイテムのデータ</param>
    /// <param name="itemAmount">取得したアイテムの数</param>
    public void TreasureFungus(BaseItemData itemData, int itemAmount = 1)
    {
        if (TreasureBlock != null && globalFlowchart != null)
        {
            // ブロック内の最初のSayコマンドを探してテキストを設定
            foreach (Command command in TreasureBlock.CommandList)
            {
                if (command is AddItemCommand addItemCommand)
                {
                    addItemCommand.SetItemData(itemData, itemAmount);
                    break; // 最初のSayを見つけたらループを抜ける
                }
            }
            globalFlowchart.ExecuteBlock(TreasureBlock);
        }
        else
        {
            Debug.LogError($"GlobalFlowchart もしくは TreasureBlock が見つかりません");
        }
    }

    #endregion

    #region Inventory Management

    /// <summary>
    /// 指定されたアイテムIDから、アイテム種別に応じた語頭（接頭辞）を取得します。
    /// </summary>
    /// <param name="itemID">アイテムのID (Enum)</param>
    /// <returns>「装備」「回復アイテム」などの文字列。該当しない場合は空文字を返します。</returns>
    public string GetItemTypePrefix(Enum itemID)
    {
        // Enumから、アイテムのタイプを判別するための内部IDを取得
        int typeNumber = EnumIDUtility.ExtractTypeID(EnumIDUtility.ToID(itemID));
        string itemPrefix = ""; // デフォルトは空

        // アイテムタイプに応じて接頭辞を決定
        switch (typeNumber)
        {
            case (int)TypeID.Blade:
            case (int)TypeID.Shoot:
                itemPrefix = "装備";
                break;
            case (int)TypeID.HealItem:
                itemPrefix = "回復アイテム";
                break;
            case (int)TypeID.StatusEnhanceItem:
                itemPrefix = "強化アイテム";
                break;
            case (int)TypeID.KeyItem:
                itemPrefix = "重要アイテム";
                break;
            case (int)TypeID.RecipeItem:
                itemPrefix = "レシピ";
                break;
        }

        return itemPrefix;
    }

    /// <summary>
    /// 指定されたIDに対応するアイテムを取得して、インベントリに保存します。
    /// </summary>
    /// <param name="ID">取得したいアイテムのID（Enum）</param>
    /// <param name="amount">取得する数量</param>
    public void AddAllTypeIDToInventory(Enum ID, int amount = 1)
    {
        //Enumから、タイプを判別する数に変更
        int typeNumber = EnumIDUtility.ExtractTypeID(EnumIDUtility.ToID(ID));
        if (GameManager.instance.savedata == null)
        {
            Debug.LogError("SaveDataが存在しません");
            return;
        }

        for (int i = 0; i < amount; i++)
        {
            switch (typeNumber)
            {
                case (int)TypeID.Blade:
                    GameManager.instance.savedata.WeaponInventoryData.AddWeapon(ID);
                    break;
                case (int)TypeID.Shoot:
                    GameManager.instance.savedata.WeaponInventoryData.AddWeapon(ID);
                    break;
                case (int)TypeID.HealItem:
                case (int)TypeID.StatusEnhanceItem:
                case (int)TypeID.KeyItem:
                    GameManager.instance.savedata.ItemInventoryData.AddItem(ID);
                    break;
                case (int)TypeID.RecipeItem:
                    GameManager.instance.savedata.RecipeData.UnlockRecipe(ID);
                    break;
                default:
                    Debug.LogError($"このID{ID}はSaveDataに保存できません");
                    break;
            }
        }

        if (amount > 0)
        {
            OnAnyItemAddedToInventory?.Invoke(); // 任意のアイテムが追加されたときのイベントを発火
        }

        string itemName = itemDataManager.GetItemNameByID(ID);
        if (itemName != "null")
        {
            GameUIManager.instance?.AddGetItemLog(itemName); // アイテムログに追加
        }
        else
        {
            Debug.LogError($"アイテム名が取得できませんでした。ID: {ID}");
        }
    }

    /// <summary>
    /// アイテムデータ（ScriptableObject）から直接アイテムを追加するオーバーロード
    /// </summary>
    /// <param name="itemData">対象のBaseItemData</param>
    /// <param name="amount">追加する数量</param>
    public void AddAllTypeIDToInventory(BaseItemData itemData, int amount = 1)
    {
        if (itemData == null)
        {
            Debug.LogError("AddAllTypeIDToInventory: itemDataがnullです。");
            return;
        }

        // 内部で既存のEnum用メソッドを呼び出すだけなので処理の重複はありません
        AddAllTypeIDToInventory(itemData.GetItemID(), amount);
    }

    /// <summary>
    /// 指定されたIDに対応するアイテムを取得して、インベントリから削除します。
    /// </summary>
    /// <param name="ID">削除したいアイテムのID（Enum）</param>
    /// <param name="amount">削除する数量</param>
    public void RemoveAllTypeIDFromInventory(Enum ID, int amount = 1)
    {
        //Enumから、タイプを判別する数に変更
        int typeNumber = EnumIDUtility.ExtractTypeID(EnumIDUtility.ToID(ID));
        if (GameManager.instance.savedata == null)
        {
            Debug.LogError("SaveDataが存在しません");
            return;
        }

        for (int i = 0; i < amount; i++)
        {
            switch (typeNumber)
            {
                case (int)TypeID.Blade:
                    GameManager.instance.savedata.WeaponInventoryData.UseWeapon(ID);
                    break;
                case (int)TypeID.Shoot:
                    GameManager.instance.savedata.WeaponInventoryData.UseWeapon(ID);
                    break;
                case (int)TypeID.HealItem:
                case (int)TypeID.StatusEnhanceItem:
                case (int)TypeID.KeyItem:
                    GameManager.instance.savedata.ItemInventoryData.UseItem(ID);
                    break;
                case (int)TypeID.RecipeItem:
                    Debug.LogWarning($"レシピアイテムはインベントリから削除できません。ID: {ID}");
                    break;
                default:
                    Debug.LogError($"このID{ID}はSaveDataに保存できません");
                    break;
            }
        }

        if (amount > 0)
        {
            OnAnyItemRemovedFromInventory?.Invoke(ID); // 任意のアイテムが削除されたときのイベントを発火
        }
    }

    /// <summary>
    /// アイテムデータ（ScriptableObject）から直接アイテムを排除するオーバーロード
    /// </summary>
    /// <param name="itemData">対象のBaseItemData</param>
    /// <param name="amount">削除する数量</param>
    public void RemoveAllTypeIDFromInventory(BaseItemData itemData, int amount = 1)
    {
        if (itemData == null)
        {
            Debug.LogError("RemoveAllTypeIDFromInventory: itemDataがnullです。");
            return;
        }

        // 内部で既存のEnum用メソッドを呼び出すだけなので処理の重複はありません
        RemoveAllTypeIDFromInventory(itemData.GetItemID(), amount);
    }

    /// <summary>
    /// 指定されたIDに対応するアイテムの所持数を取得します。
    /// </summary>
    /// <param name="ID">取得したいアイテムのID（Enum）</param>
    /// <returns>指定されたアイテムの所持数</returns>
    public int GetAllTypeIDToAmount(Enum ID)
    {
        // Enumから、タイプを判別する数に変更
        int typeNumber = EnumIDUtility.ExtractTypeID(EnumIDUtility.ToID(ID));
        int amount = 0;

        if (GameManager.instance.savedata == null)
        {
            Debug.LogError("SaveDataが存在しません");
            return 0;
        }

        // SaveDataが存在しない場合は警告を出して0を返す
        switch (typeNumber)
        {
            case (int)TypeID.Blade:
            case (int)TypeID.Shoot:
                amount = GameManager.instance.savedata.WeaponInventoryData.GetWeaponAmount(ID);
                break;
            case (int)TypeID.HealItem:
                amount = GameManager.instance.savedata.ItemInventoryData.GetItemAmount(ID);
                break;
            case (int)TypeID.StatusEnhanceItem:
                amount = GameManager.instance.savedata.ItemInventoryData.GetItemAmount(ID);
                break;
            case (int)TypeID.KeyItem:
                amount = GameManager.instance.savedata.ItemInventoryData.GetItemAmount(ID);
                break;
            case (int)TypeID.RecipeItem:
                amount = GameManager.instance.savedata.RecipeData.GetRecipeAmount(ID);
                break;
            default:
                Debug.LogError($"このID{ID}は数を取得できません");
                break;
        }

        return amount;
    }

    /// <summary>
    /// レシピを解放（取得）します。宝箱やイベントから呼び出される想定です。
    /// </summary>
    /// <param name="recipeID">解放するレシピのID(Enum)</param>
    public void UnlockRecipe(Enum recipeID)
    {
        if (savedata == null)
        {
            Debug.LogError("SaveDataが存在しません");
            return;
        }

        // 既に解放済みかチェック
        if (!savedata.RecipeData.IsRecipeUnlocked(recipeID))
        {
            savedata.RecipeData.UnlockRecipe(recipeID);

            // UI通知（既存のアイテム取得ログの仕組みを流用）
            // string recipeName = itemDataManager.GetItemNameByID(recipeID);
            // GameUIManager.instance?.AddGetItemLog($"{recipeName}のレシピ");

            // Debug.Log($"レシピID:{recipeID} を解放しました！");
        }
        else
        {
            // // 宝箱等で重複取得した際の処理（代替アイテムを渡す等）が必要ならここに記述
            // Debug.Log($"レシピID:{recipeID} は既に解放済みです。");
        }
    }

    /// <summary>
    /// BaseItemDataから直接レシピを解放するオーバーロード
    /// </summary>
    public void UnlockRecipe(BaseItemData itemData)
    {
        if (itemData == null)
        {
            Debug.LogWarning("UnlockRecipe: itemDataがnullです。");
            return;
        }
        UnlockRecipe(itemData.GetItemID());
    }

    /// <summary>
    /// アイテムデータ（ScriptableObject）から直接所持数を取得するオーバーロード
    /// </summary>
    /// <param name="itemData">対象のBaseItemData</param>
    /// <returns>所持数</returns>
    public int GetAllTypeIDToAmount(BaseItemData itemData)
    {
        if (itemData == null)
        {
            Debug.LogError("GetAllTypeIDToAmount: itemDataがnullです。");
            return 0;
        }

        // 内部で既存のEnum用メソッドを呼び出すだけなので処理の重複はありません
        return GetAllTypeIDToAmount(itemData.GetItemID());
    }

    /// <summary>
    /// デバッグ用：データベース内のすべての重要アイテム（KeyItem）を指定個数入手します。
    /// </summary>
    /// <param name="amount">入手する個数</param>
    public void AddAllKeyItems(int amount = 1)
    {
        if (keyItemDatabase == null || keyItemDatabase.keyItems == null)
            return;

        foreach (var keyItem in keyItemDatabase.keyItems)
        {
            AddAllTypeIDToInventory(keyItem.itemID, amount);
        }
    }

    /// <summary>
    /// デバッグ用：データベース内のすべての回復アイテム（HealItem）を指定個数入手します。
    /// </summary>
    /// <param name="amount">入手する個数</param>
    public void AddAllHealItems(int amount = 1)
    {
        if (healItemDatabase == null || healItemDatabase.healItems == null)
            return;

        foreach (var healItem in healItemDatabase.healItems)
        {
            AddAllTypeIDToInventory(healItem.itemID, amount);
        }
    }

    /// <summary>
    /// デバッグ用：データベース内のすべての強化アイテム（StatusEnhanceItem）を指定個数入手します。
    /// <param name="amount">入手する個数</param>
    /// </summary>
    public void AddAllStatusEnhanceItems(int amount = 1)
    {
        if (
            statusEnhanceItemDatabase == null
            || statusEnhanceItemDatabase.statusEnhanceItems == null
        )
            return;

        foreach (var enhanceItem in statusEnhanceItemDatabase.statusEnhanceItems)
        {
            AddAllTypeIDToInventory(enhanceItem.itemID, amount);
        }
    }

    /// <summary>
    /// デバッグ用：データベース内のすべてのレシピアイテム（RecipeItem）を指定個数入手します。
    /// <param name="amount">入手する個数</param>
    /// </summary>
    public void AddAllRecipeItems(int amount = 1)
    {
        if (recipeItemDatabase == null || recipeItemDatabase.recipeItems == null)
            return;

        foreach (var recipeItem in recipeItemDatabase.recipeItems)
        {
            AddAllTypeIDToInventory(recipeItem.itemID, amount);
        }
    }

    #endregion

    #region Tips Management

    /// <summary>
    /// TipsInfoDatabaseからTipsの正しい表示順を読み込み、辞書としてキャッシュします。
    /// </summary>
    private void InitializeTipsSortOrderMap()
    {
        tipsSortOrderMap = new Dictionary<int, int>();
        if (tipsInfoDatabase == null)
        {
            Debug.LogError("TipsInfoDatabaseがマネージャーに設定されていません。");
            return;
        }

        // データベースのリストのインデックス（i）が、そのまま並び順の優先度となる
        for (int i = 0; i < tipsInfoDatabase.tips.Count; i++)
        {
            // Enumをintに変換してIDを取得
            int tipsId = (int)tipsInfoDatabase.tips[i].tipsName;
            if (!tipsSortOrderMap.ContainsKey(tipsId))
            {
                tipsSortOrderMap.Add(tipsId, i);
            }
        }
    }

    /// <summary>
    /// 所持しているTipsのリストを、データベースの定義順に並び替えます。
    /// </summary>
    public void SortUnlockedTips()
    {
        var tipsData = savedata?.TipsData;
        if (tipsData == null)
            return;

        // LINQのOrderByを使い、キャッシュした辞書の並び順に従ってリストをソート
        tipsData.unlockedTips = tipsData
            .unlockedTips.OrderBy(tip =>
                // 辞書からTipsIDに対応する並び順の番号を取得する
                // もし辞書にないTipsの場合、int.MaxValueを返すことでリストの末尾に配置する
                tipsSortOrderMap.TryGetValue(tip.TipsID, out int order)
                    ? order
                    : int.MaxValue
            )
            .ToList();
    }

    #endregion
}
