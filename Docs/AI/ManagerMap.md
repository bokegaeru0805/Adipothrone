# Manager Map

## Manager一覧

### Confirmed

| Manager | コードから確認できる取得・lifecycle | 主責務 | 主な直接参照（コード上） |
|---|---|---|---|
| `PersistentManagers` | singleton。`Awake()`で自身のGameObjectに`DontDestroyOnLoad`を適用 | 複数のglobal Managerを子に持つ親として意図されていることがコードコメントに記載 | C#内で特定の子Managerを取得・登録する処理はない |
| `GameManager` | static `instance` | global state、inventory、Fungus取得通知 | `ItemDataManager`, `InputManager`, `SaveLoadManager`, `GameUIManager`, `SkillManager`へのstatic instance参照 |
| `SaveLoadManager` | static `instance` | ES3 save/load、scene、settings | `GameManager`, `FlagManager`, `PlayerManager`, `WeaponManager`, audio/time/fadeへのstatic instance参照 |
| `FlagManager` | static `instance` | enum別bool/int/key flag | `FlagData`; UnityEvent wrapperを公開 |
| `ItemDataManager` | static `instance` | 各item database横断検索 | 各Database ScriptableObjectへのInspector参照 |
| `WeaponManager` | singleton | inventory/equipment整合 | `GameManager` event/save data |
| `SkillManager` | singleton | skill解放・point | `GameManager.savedata.SkillData` |
| `BGMManager` | singleton | CRIWARE BGM | `SaveLoadManager.Settings` |
| `SEManager` | singleton | CRIWARE SE category別再生 | `SaveLoadManager.Settings`, `SeCueDatabase` |
| `InputManager` | singleton | legacy Input APIの窓口 | `InputSettings` |
| `TimeManager` | singleton | pause、enemy pause、hit stop、skip scale | `UIManager` |
| `GlobalFlowchartController` | static `instance` + 自身を`DontDestroyOnLoad` | global Fungus Flowchart | `Fungus.Flowchart`へのInspector参照 |
| `GlobalVolumeManager` | static `instance` | URP volume profile | Volume assetsへのInspector参照 |
| `FastTravelManager` | component参照 | warp/death travel | Player/Game/Flag/Flowchart/Fade |
| `TimelineSkipManager` | singleton | Timeline/Fungus skip | Input/Time/Player/SE/Fade/FungusSkip |
| `PlayerManager` | static `instance`。コードコメント上scene-local | player status/death/item/movement facade | static instance参照、Inspector参照、`PersistentManagers.GetComponentInChildren<FastTravelManager>()` |
| `PlayerBodyManager` | static `instance` | body state | 同一GameObjectの`PlayerManager`を`GetComponent`し、`OnChangeWP`をevent購読。`GameConstants`を参照 |
| `PlayerEffectManager` | singleton | timed effectとsave反映 | Game/UI/SE |
| `PlayerLevelManager` | singleton | level/experience | Game/GameUI/SE |
| `PlayerStatusLevelManager` | PlayerManagerから参照 | status上限強化 | Player status data |
| `UIManager` | singleton、scene-local cleanup | gameplay menu panel stack | Player/Level/Input/Time/Save |
| `GameUIManager` | singleton | HUD/log/level-up | PlayerManager |
| `ShopUIManager` | singleton | shop state、購入/売却 | Player/Game/ItemData/Input/Fungus |
| `HealItemPreviewUIManager` | singleton | item効果preview | Player/PlayerEffect |
| `TitleUIManager` | singleton、destroy時clear | title panel stack | Save/Input |
| `GameOverUIManager` | singleton、destroy時clear | game-over画面 | Time/Input/Save/BGM/SE/ObjectPooler |
| `CameraManager` | singleton | camera move/shake | Cinemachine/DOTween、Player位置 |

根拠は主に `Assets/Scripts/Manager/*.cs`。表はC#上の取得・参照を示し、GameObjectの親子関係を示さない。Prefab候補は `Assets/Prefabs/Global/PersistentManagers.prefab`, `LocalManagers.prefab`, `UIManagers.prefab` と `Assets/Prefabs/Managers/` だが、実際の階層は未確認。

## コード上の参照関係図

### Confirmed

この図はC#上の参照方向を示す。GameObjectの所有関係、親子関係、Prefab階層は示さない。

```text
GameManager --static instance--> ItemDataManager / InputManager / SaveLoadManager
GameManager --static instance--> GameUIManager / SkillManager
GameManager --field-----------> current SaveData

SaveLoadManager --static instance--> GameManager / FlagManager / PlayerManager
SaveLoadManager --static instance--> WeaponManager / BGMManager / SEManager / TimeManager
SaveLoadManager --method call-----> ES3 API

PlayerManager --static instance--> GameManager / CameraManager / TimeManager / SEManager
PlayerManager --GetComponent----> PlayerStatusLevelManager (same GameObjectを期待するコード)
PlayerManager --Inspector ref---> database assets
PlayerManager --GetComponentInChildren via PersistentManagers.instance--> FastTravelManager

PlayerBodyManager --GetComponent--> PlayerManager (same GameObjectを期待するコード)
PlayerBodyManager --event subscribe--> PlayerManager.OnChangeWP

GlobalFlowchartController --Inspector ref--> Fungus.Flowchart
Fungus Custom Commands --static instance / method caller--> PlayerBodyManager
```

- `GameManager` はinventory event (`OnInventoryUpdated`, `OnWeaponInventoryUpdated`等) を公開し、UI/Weapon系が購読する。
- `PlayerManager` は死亡/復活/HP/WP/quick-slot等のeventを公開し、game UIやeffectが購読する。
- `SaveLoadManager` はロード後に `GameManager.savedata` を置換し、Weapon、Flag、PlayerEffect等へ再同期する。

### Incoming caller

- `SetBodyStateCommand`, `StepBodyStateCommand`, `TalkStartCommand` は `PlayerBodyManager.instance` を参照し、公開APIまたは状態を利用する (`Assets/Scripts/FungusCustom/`)。これは `PlayerBodyManager` からFungusへの直接依存ではなく、Fungus Custom Command側からのmethod caller/static instance参照である。

### Inferred

- `GameManager` と `SaveLoadManager` は多数のManagerから参照され、Player/UI/進行/音声へ広く影響する構造とみられる。
- singleton初期化順はPrefab hierarchyとUnity lifecycleに依存する。null checkや`Start`取得、`WaitUntil`が混在するのは順序差を吸収する意図とみられる。

## Singleton / DontDestroyOnLoad

### Confirmed

- `instance` singletonを持つ主なクラス: Game、SaveLoad、Flag、ItemData、Weapon、Skill、BGM、SE、Input、Time、GlobalFlowchart、GlobalVolume、TimelineSkip、Player、PlayerBody、PlayerEffect、PlayerLevel、UI、GameUI、ShopUI、TitleUI、GameOverUI、Camera、PersistentManagers。
- 実行中の `DontDestroyOnLoad`: `PersistentManagers`, `GlobalFlowchartController`, `FadeCanvas`。Developerの `DebugSceneLoaderButton`, `DebugSEManager` にも実行箇所がある。
- 多くのManager内の個別 `DontDestroyOnLoad` はコメントアウトされ、コメントには親オブジェクトの永続化を前提とする記述がある。各Managerが実際に`PersistentManagers`配下にあるかは未確認。
- `PlayerManager` はscene-localとコメントで明記。`UIManager`, `TitleUIManager`, `GameOverUIManager` はdestroy時にinstanceをclearする実装を持つ。

### Unknown

- `PersistentManagers.prefab` の実際の子階層と、どのManagerがその配下にあるか。
- `LocalManagers.prefab`, `UIManagers.prefab` を含む各Prefabの親子関係、全Sceneでの重複配置、script execution order、起動sceneごとの初期化順。
