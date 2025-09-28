using System.Collections.Generic;
using UnityEngine;

public class FlagBasedObjectManager_Chapter1 : MonoBehaviour
{
    [Header("村の右端の移動制限エリア")]
    [SerializeField]
    private GameObject MoveLimitZone_Village_Right = null;

    [Header("村のショップの女の子")]
    [SerializeField]
    private GameObject Village_ShopGirl = null;

    [SerializeField]
    private Sprite villagerShopGirlNormal = null;

    [SerializeField]
    private Sprite villagerShopGirlBodyType1 = null;

    [SerializeField]
    private Sprite villagerShopGirlBodyType2 = null;

    [SerializeField]
    private Sprite villagerShopGirlBodyType3 = null;

    [Header("村の女の子1")]
    [SerializeField]
    private GameObject Village_Girl1 = null;

    [SerializeField]
    private Sprite villagerGirl1BodyType1 = null;

    [SerializeField]
    private Sprite villagerGirl1BodyType2 = null;

    [Header("村の女の子2")]
    [SerializeField]
    private GameObject Village_Girl2 = null;

    [SerializeField]
    private Sprite villagerGirl2BodyType1 = null;

    [SerializeField]
    private Sprite villagerGirl2BodyType2 = null;

    [Header("村の男性1")]
    [SerializeField]
    private GameObject Village_Man1 = null;

    [Header("村の男性2")]
    [SerializeField]
    private GameObject Village_Man2 = null;

    [Header("岩のがれき")]
    [SerializeField]
    private GameObject RockDebris = null;

    [Header("ボス戦の移動制限エリア")]
    [SerializeField]
    private GameObject MoveLimitZone_BossFight = null;

    [Header("ボス撃破後のエリア遷移")]
    [SerializeField]
    private GameObject AreaTransition_BossDefeated = null;

    private FlagManager flagManager = null;
    private Dictionary<string, GameObject> namedObjects = new Dictionary<string, GameObject>();
    private SpriteRenderer villageShopGirlSpriteRenderer = null;
    private SpriteRenderer villageGirl1SpriteRenderer = null;
    private SpriteRenderer villageGirl2SpriteRenderer = null;

    private void Awake()
    {
        namedObjects.Add("MoveLimitZone_Village_Right", MoveLimitZone_Village_Right);
        namedObjects.Add("Village_ShopGirl", Village_ShopGirl);
        namedObjects.Add("Village_Girl1", Village_Girl1);
        namedObjects.Add("Village_Girl2", Village_Girl2);
        namedObjects.Add("Village_Man1", Village_Man1);
        namedObjects.Add("Village_Man2", Village_Man2);
        namedObjects.Add("RockDebris", RockDebris);
        namedObjects.Add("AreaTransition_BossDefeated", AreaTransition_BossDefeated);
        namedObjects.Add("MoveLimitZone_BossFight", MoveLimitZone_BossFight);

        CheckAssignedObject(MoveLimitZone_Village_Right);
        CheckAssignedObject(Village_ShopGirl);
        CheckAssignedSprite(villagerShopGirlNormal);
        CheckAssignedSprite(villagerShopGirlBodyType1);
        CheckAssignedSprite(villagerShopGirlBodyType2);
        CheckAssignedSprite(villagerShopGirlBodyType3);
        CheckAssignedObject(Village_Girl1);
        CheckAssignedSprite(villagerGirl1BodyType1);
        CheckAssignedSprite(villagerGirl1BodyType2);
        CheckAssignedObject(Village_Girl2);
        CheckAssignedSprite(villagerGirl2BodyType1);
        CheckAssignedSprite(villagerGirl2BodyType2);
        CheckAssignedObject(Village_Man1);
        CheckAssignedObject(Village_Man2);
        CheckAssignedObject(RockDebris);
        CheckAssignedObject(AreaTransition_BossDefeated);
        CheckAssignedObject(MoveLimitZone_BossFight);

        villageShopGirlSpriteRenderer = Village_ShopGirl.GetComponent<SpriteRenderer>();
        villageGirl1SpriteRenderer = Village_Girl1.GetComponent<SpriteRenderer>();
        villageGirl2SpriteRenderer = Village_Girl2.GetComponent<SpriteRenderer>();
    }

    // <summary>
    /// Inspectorで設定されたGameObjectがnullでないかを確認し、ログに出力します。
    /// </summary>
    /// <param name="obj">チェック対象のGameObject</param>
    /// <param name="objName">GameObjectの名前（nameof()で渡すことを推奨）</param>
    private void CheckAssignedObject(GameObject obj)
    {
        if (obj == null)
        {
            Debug.LogError(
                $"{nameof(obj)}が設定されていません。Inspectorで設定してください。",
                this
            );
        }

        foreach (var pair in namedObjects)
        {
            if (pair.Value == obj)
            {
                if (!obj.name.Contains(pair.Key))
                {
                    Debug.LogWarning(
                        $"{pair.Key}のGameObjectが正しく設定されていません。名前に'{pair.Key}'を含めてください。",
                        this
                    );
                }
                break;
            }
        }
    }

    private void CheckAssignedSprite(Sprite sprite)
    {
        if (sprite == null)
        {
            Debug.LogError(
                $"{nameof(sprite)}が設定されていません。Inspectorで設定してください。",
                this
            );
        }
    }

    private void Start()
    {
        flagManager = FlagManager.instance;
        if (flagManager == null)
        {
            Debug.LogWarning(
                "FlagManager.instance が見つかりません。FlagBasedObjectManagerの機能が制限される可能性があります。",
                this
            );
        }

        // FlagManager.instanceがnullでないことを確認してからイベントに登録する
        if (FlagManager.instance != null)
        {
            //FlagManager.instance.OnFlagChanged += UpdateAllFlagDependentStates; // フラグが変更されたときに呼び出す
        }
        UpdateAllFlagDependentStates(); // オブジェクトが有効になった際の初期状態を更新
    }

    private void OnDisable()
    {
        // FlagManager.instanceがnullでないことを確認してからイベント解除する
        if (FlagManager.instance != null)
        {
            //FlagManager.instance.OnFlagChanged -= UpdateAllFlagDependentStates; // フラグが変更されたときのイベントを解除
        }
    }

    /// <summary>
    /// ゲームのフラグの状態に基づいて、関連するオブジェクトの表示状態や位置を全て更新します。
    /// OnFlagChangedイベントから呼び出されます。
    /// </summary>
    private void UpdateAllFlagDependentStates()
    {
        if (flagManager == null)
        {
            flagManager = FlagManager.instance;
            if (flagManager == null)
            {
                Debug.LogWarning(
                    "FlagManagerが見つかりません。FlagBasedObjectManagerの機能が制限される可能性があります。",
                    this
                );
                return; // FlagManagerがない場合は処理を中断
            }
        }

        UpdateVillageRightMoveLimitZoneState(); // 村の右端の移動制限エリアの表示/非表示を更新
        UpdateVillageShopGirl(); // 村のショップの女の子の位置を更新
        UpdateVillageGirl1(); // 村の女の子1の位置を更新
        UpdateVillageGirl2(); // 村の女の子2の位置を更新
        UpdateVillageMan1(); // 村の男性1の位置を更新
        UpdateVillageMan2(); // 村の男性2の位置を更新
        UpdateRockDebris(); // 岩のがれきの表示/非表示を更新
        UpdateAreaTransitionState(); // ボス撃破後のエリア遷移の表示/非表示を更新
        UpdateBossFightMoveLimitZoneState(); // ボス戦の移動制限エリアの表示/非表示を更新
    }

    /// <summary>
    /// 村の右端の移動制限エリアの表示状態を、フラグに基づいて更新します。
    /// </summary>
    private void UpdateVillageRightMoveLimitZoneState()
    {
        if (MoveLimitZone_Village_Right == null)
        {
            Debug.LogError("MoveLimitZone_Village_Rightが設定されていません", this);
            return;
        }

        if (flagManager.GetBoolFlag(Chapter1TriggeredEvent.WellQuestComplete))
        {
            TrySetActive(false, MoveLimitZone_Village_Right);
        }
        else
        {
            TrySetActive(true, MoveLimitZone_Village_Right);
        }
    }

    /// <summary>
    /// 村の女の子1のワールド座標を、フラグに基づいて更新します。
    /// </summary>
    private void UpdateVillageShopGirl()
    {
        if (Village_ShopGirl == null)
        {
            Debug.LogError("Village_ShopGirlが設定されていません", this);
            return;
        }

        if (flagManager.GetBoolFlag(Chapter1TriggeredEvent.RiverBossDefeated))
        {
            villageShopGirlSpriteRenderer.sprite = villagerShopGirlBodyType3;
            villageShopGirlSpriteRenderer.transform.position = new Vector2(235f, -100f);
        }
        else if (flagManager.GetBoolFlag(Chapter1TriggeredEvent.UpperRiverReached))
        {
            villageShopGirlSpriteRenderer.sprite = villagerShopGirlBodyType2;
        }
        else if (flagManager.GetBoolFlag(Chapter1TriggeredEvent.WellEnemyDefeated))
        {
            villageShopGirlSpriteRenderer.sprite = villagerShopGirlBodyType1;
        }
        else
        {
            villageShopGirlSpriteRenderer.sprite = villagerShopGirlNormal;
        }
    }

    /// <summary>
    /// 村の女の子1のワールド座標を、フラグに基づいて更新します。
    /// </summary>
    private void UpdateVillageGirl1()
    {
        if (Village_Girl1 == null)
        {
            Debug.LogError("Village_Girl1が設定されていません", this);
            return;
        }

        if (flagManager.GetBoolFlag(Chapter1TriggeredEvent.UpperRiverReached))
        {
            villageGirl1SpriteRenderer.sprite = villagerGirl1BodyType2;
        }
        else
        {
            villageGirl1SpriteRenderer.sprite = villagerGirl1BodyType1;
        }
    }

    /// <summary>
    /// 村の女の子2のワールド座標を、フラグに基づいて更新します。
    /// </summary>
    private void UpdateVillageGirl2()
    {
        if (Village_Girl2 == null)
        {
            Debug.LogError("Village_Girl2が設定されていません", this);
            return;
        }

        if (flagManager.GetBoolFlag(Chapter1TriggeredEvent.UpperRiverReached))
        {
            villageGirl2SpriteRenderer.sprite = villagerGirl2BodyType2;
        }
        else
        {
            villageGirl2SpriteRenderer.sprite = villagerGirl2BodyType1;
        }
    }

    /// <summary>
    /// 村の男性1のワールド座標を、フラグに基づいて更新します。
    /// </summary>
    private void UpdateVillageMan1()
    {
        if (Village_Man1 == null)
        {
            Debug.LogError("Village_Man1が設定されていません", this);
            return;
        }

        if (
            flagManager.GetBoolFlag(Chapter1TriggeredEvent.RockDestructionRequested)
            && !flagManager.GetBoolFlag(Chapter1TriggeredEvent.RiverBossDefeated)
        )
        {
            Village_Man1.transform.position = new Vector2(20f, 0f);
        }
        else
        {
            Village_Man1.transform.position = new Vector2(59f, 0f);
        }
    }

    /// <summary>
    /// 村の男性2のワールド座標を、フラグに基づいて更新します。
    /// </summary>
    private void UpdateVillageMan2()
    {
        if (Village_Man2 == null)
        {
            Debug.LogError("Village_Man2が設定されていません", this);
            return;
        }

        if (flagManager.GetBoolFlag(Chapter1TriggeredEvent.RiverBossDefeated))
        {
            Village_Man2.transform.position = new Vector2(59f, 0f);
        }
        else if (flagManager.GetBoolFlag(Chapter1TriggeredEvent.UpperRiverReached))
        {
            Village_Man2.transform.position = new Vector2(-6f, 0f);
        }
        else
        {
            Village_Man2.transform.position = new Vector2(2.7f, 0f);
        }
    }

    /// <summary>
    /// 岩のがれきのワールド座標を、フラグに基づいて更新します。
    /// </summary>
    private void UpdateRockDebris()
    {
        if (RockDebris == null)
        {
            Debug.LogError("RockDebrisが設定されていません", this);
            return;
        }

        if (flagManager.GetBoolFlag(Chapter1TriggeredEvent.RockDestructionRequested))
        {
            TrySetActive(false, RockDebris);
        }
        else
        {
            TrySetActive(true, RockDebris);
        }
    }

    /// <summary>
    /// ボス撃破後のエリア遷移の表示状態を、フラグに基づいて更新します。
    /// </summary>
    private void UpdateAreaTransitionState()
    {
        if (AreaTransition_BossDefeated == null)
        {
            Debug.LogError("AreaTransition_BossDefeatedが設定されていません", this);
            return;
        }

        if (
            flagManager.GetBoolFlag(Chapter1TriggeredEvent.RiverBossDefeated)
            && !flagManager.GetBoolFlag(Chapter1TriggeredEvent.ShopGirlMissing)
        )
        {
            TrySetActive(true, AreaTransition_BossDefeated);
        }
        else
        {
            TrySetActive(false, AreaTransition_BossDefeated);
        }
    }

    /// <summary>
    /// ボス戦の移動制限エリアの表示状態を、フラグに基づいて更新します。
    /// </summary>
    private void UpdateBossFightMoveLimitZoneState()
    {
        if (MoveLimitZone_BossFight == null)
        {
            Debug.LogError("MoveLimitZone_BossFightが設定されていません", this);
            return;
        }

        if (
            flagManager.GetBoolFlag(Chapter1TriggeredEvent.RiverBossAppear)
            && !flagManager.GetBoolFlag(Chapter1TriggeredEvent.RiverBossDefeated)
        )
        {
            TrySetActive(true, MoveLimitZone_BossFight);
        }
        else
        {
            TrySetActive(false, MoveLimitZone_BossFight);
        }
    }

    private void TrySetActive(bool isActive, GameObject obj)
    {
        if (obj == null)
        {
            Debug.LogError(
                $"{nameof(obj)}が設定されていません。Inspectorで設定してください。",
                this
            );
            return;
        }

        if (obj.activeSelf != isActive)
        {
            obj.SetActive(isActive);
        }
    }
}
