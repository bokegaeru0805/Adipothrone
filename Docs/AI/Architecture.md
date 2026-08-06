# Architecture

分類は **Confirmed / Inferred / Unknown**。根拠は相対パスとクラス名で示す。

## 全体像

### Confirmed

- グローバル状態の中心は `GameManager.savedata` (`Assets/Scripts/Manager/GameManager.cs`, `GameManager`)。`SaveData` はPlayerStatus、武器/アイテム、フラグ以外の進行データ等を集約する (`Assets/Scripts/SaveData/SaveData.cs`, `SaveData`)。
- `PersistentManagers.Awake()` は `DontDestroyOnLoad(gameObject)` を呼ぶ。クラスのコードコメントでは、子にglobal Managerを持つ親オブジェクトとして説明されている (`Assets/Scripts/Manager/PersistentManagers.cs`, `PersistentManagers`)。
- `PlayerManager` はシーンごとに配置し、永続化しない設計が明記される (`Assets/Scripts/Manager/PlayerManager.cs`)。
- 会話/イベントは Fungus Flowchart、カスタム `Command`、Flag、FieldEventを横断する。根拠: `Assets/Scripts/FungusCustom/`, `Assets/Scripts/FIeldEvents/`, `Assets/Scripts/Manager/GlobalFlowchartController.cs`。

### Inferred

- 実行時の大枠は「永続サービス層 → シーンローカルなPlayer/UI/Camera → GameObject固有controller → ScriptableObjectデータ」という構造。
- Manager間はDIコンテナではなく、静的 `instance`、Inspector参照、`GetComponentInChildren`、イベント購読を混在させて接続している。
- `PersistentManagers` はprojectの永続化rootとして意図された可能性があるが、実際の利用範囲はPrefab階層とScene配置の確認が必要。

## Player

### Confirmed

- `PlayerManager` はHP/WP、死亡、回復、所持金、アイテム使用、位置、操作ロックを担当し、`GameManager.savedata.PlayerStatus` を読み書きする (`Assets/Scripts/Manager/PlayerManager.cs`)。
- `Heroin_move` は入力に応じた移動・ジャンプ、接地判定、ダメージ時の無敵、PlayerManagerの死亡eventへの応答などを実装する (`Assets/Scripts/Players/Heroin_move.cs`, `Heroin_move`)。
- `Robot_move` はPlayerManagerのWP変更eventを購読する処理を持ち、`Robot_blade_move` と `FaboProjectileController` にはblade・projectile関連の挙動が実装されている (`Assets/Scripts/Players/`)。
- `PlayerBodyManager` は体型状態、`PlayerEffectManager` はバフ/状態効果、`PlayerLevelManager` と `PlayerStatusLevelManager` はレベル/能力成長を分担する (`Assets/Scripts/Manager/`)。
- `PlayerShieldController` / `PlayerShieldUIController`、`PlayerInteractionBubble`、`PlayerBuffEffect`、`PinchEffectManager` が表示・補助機能を分担する (`Assets/Scripts/Players/`)。

### Inferred

- `PlayerManager` がdomain service兼scene facadeであり、移動本体は `Heroin_move`、永続データは `GameManager.savedata` に分離されている。
- 実装量と処理内容から `Heroin_move` はプレイヤー操作の主要controllerとみられるが、`PlayerManager`, `Robot_move`, `Robot_blade_move` との正式な責務境界は文書やC#だけでは確定しない。

### Unknown

- Player Prefab上での `Heroin_move`, `PlayerManager`, `Robot_move`, `Robot_blade_move` の配置・参照と、Unity Editor上で意図された正式な責務境界。

## Enemy

### Confirmed

- 継承軸は `PoolableObject` → `CharacterHealth` → `EnemyHealth` / `BossHealth` / `ObjectHealth` / `UniqueBossHealth` (`Assets/Scripts/Common/PoolableObject.cs`, `Assets/Scripts/Enemies/CharacterHealth.cs`)。
- `CharacterHealth` は `IDamageable`, `IDroppable`, `IDefeatable` を実装し、HP、被弾、死亡、ドロップ、討伐記録、共通視覚効果を管理する。
- 通常敵の行動は `Assets/Scripts/Enemies/MoveController/` の個別controllerに分かれ、多くが `IEnemyResettable` を実装する。
- `EnemyActivator` がエリア内の敵activation、`ObjectPooler` がPersistent/Sceneの2系統のpool、`ContactDamageController` が接触ダメージを担当する。
- `EnemyData` / `EnemyDatabase` が敵ID、能力、drop等をScriptableObject化する (`Assets/Scripts/Datas/EnemyData.cs`)。

### Inferred

- 共通のhealth/death/pooling処理を基底へ寄せつつ、AIは敵種別ごとのMonoBehaviourに直接実装する方式。

## Weapon / Item / Skill / Shop

### Confirmed

- Item継承: `BaseItemData` → `HealItemData`, `StatusEnhanceItemData`, `MaterialItemData`, `KeyItemData`, `RecipeItemData`, および `WeaponData` → `BladeWeaponData` / `ShootWeaponData` (`Assets/Scripts/Datas/`)。
- 各データ型は対応Database ScriptableObjectを持つ。IDは `IItemIDProvider.GetItemID()` とEnum/int変換 (`EnumIDUtility`) で横断的に扱う。
- `WeaponManager` は所持武器と装備参照の再構築、`ItemDataManager` は各Database横断検索、`SkillManager` は解放とポイント、`ShopUIManager` は購入/売却UIを担当する。
- 実セーブ形は `InventoryWeaponData`, `InventoryItemData`, `RecipeSaveData`, `SkillSaveData` (`Assets/Scripts/SaveData/`)。

## UI

### Confirmed

- `UIManager`, `TitleUIManager`, `GameOverUIManager` が `IPanelStackManager` を実装し、画面別panel stackを管理する。
- `GameUIManager` はgame HUD、取得ログ、level-up表示。参照集約は `GameUIRefs`, `MenuUIRefs`, `ShopUIRefs`, `GameOverUIRefs` (`Assets/Scripts/UIs/`)。
- UI機能は Item、Equip、Skill、Craft、Shop、SaveLoad、FastTravel、EnemyDex、Tips、Settings、ProgressLog に分割される。

## Save / Audio / Camera / Event

### Confirmed

- `SaveLoadManager` がEasy Save 3 (`ES3`) を介してslot、autosave、settings、scene復帰、各Managerへのデータ反映を統括する (`Assets/Scripts/Manager/SaveLoadManager.cs`)。
- `BGMManager`, `SEManager`, `ObjectSeManager`, `CriAtomSePlayer` がCRIWARE音声を扱う (`Assets/Scripts/Manager/`, `Assets/Scripts/Sound/`)。
- `CameraManager`, `CameraMoveArea`, `CameraBoundaryChecker` がCinemachine、DOTween、URP camera/volume連携を扱う。
- Eventは Fungus custom command、`BaseFieldEvent`派生、`FlagDrivenState`, `GimmickSwitch`, `LotteryGameManager`, Timeline custom track に分散する。

## 基底型・interface・utility

### Confirmed

- 基底型: `PoolableObject`, `CharacterHealth`, `BaseItemData`, `WeaponData`, `BaseFieldEvent`, `BasePortraitController`, `ItemPanelActiveBase`。
- 主なinterface: `IDamageable`, `IDroppable`, `IDefeatable`, `IEnemyResettable`, `IItemIDProvider`, `IItemAssignable`, `IItemPromptHandler`, `IPanelActive`, `IPanelStackManager`, `IPageNavigable`, `IShopConversation` (`Assets/Scripts/Interfaces/`)。
- 共通utility: `EnumIDUtility` (Enum/int ID), `FungusHelper` (block実行), `UIUtility` (UI補助), `GameConstants` (tag・値・BodyState等)。

## C#だけでは確定できないこと

### Unknown

- 各Scene/Prefabでの実コンポーネント配置、SerializeFieldの割当、UnityEvent listener、Fungus Flowchartのblock/command順、Animator/Animation Eventの紐付け。
- ScriptableObject assetに保存された具体値が実行時にどのPrefabへ割り当てられるか。
- `PersistentManagers.prefab` の実際の子Manager、各Sceneでの同Prefabまたは同種GameObjectの配置。
