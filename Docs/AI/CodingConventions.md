# Coding Conventions

## Confirmed

### Naming

- 確認した主要クラスでは、bool field/propertyに `is...` / `Is...` (`isTalking`, `isEnableSave`, `IsDefeated`) が頻出する。ただし全自作C#を件数集計した結果ではなく、project全体で必須の実装規則とは断定しない。`useManualHP`, `enableShield`, `linkToShieldController` 等の目的語/動詞prefixも存在する。根拠: `GameManager`, `CharacterHealth`, `SaveLoadManager`。
- 調査したManager系の複数クラスでは、singleton property/fieldに小文字 `instance` が使われている。これはproject全体を件数集計した規則ではなく、`ObjectPooler` の `PersistentInstance` / `SceneInstance` などの反例がある。
- 確認した主要クラスではpublic property/eventのPascalCaseとprivate fieldのcamelCaseが複数確認された。project全体を集計した必須規則ではなく、`_playerManager`, `_sePlayer` のunderscore形式や `DropItemPrefab`, `TreasureBlock`, `ItemInventory` 等の反例がある。
- ファイル/クラスには既存の綴り揺れがある: `Heroin_move`, `Robot_move`, `FIeldEvents`, `Portarit`, `Objetcs`, `ImputManager.prefab`, `Gloval Volume`。変更はしていない。

### Enum / ID

- item/weapon/flag等のEnumは明示的な数値を持つ例が多い。`ItemRank` は100刻み、各ID Enumはtype情報を含むintとして `EnumIDUtility` で変換する。
- 一方、private state enumや `PoolType` 等には暗黙値の例もあり、「全enumに明示値」は一貫していない。根拠: `Assets/Scripts/Enemies/MoveController/`, `Assets/Scripts/Common/PoolableObject.cs`。

### Inspector

- `[SerializeField]`, `[Header]`, `[Tooltip]` が広範に使われ、private/protected fieldをInspector設定する。
- NaughtyAttributesの `[ShowIf]`, `[AllowNesting]`, `[ShowAssetPreview]` 等で条件表示と検証を補助する。根拠: `CharacterHealth`, `EnemyData`, `BaseItemData`, `FlagDrivenState`。
- Data定義はScriptableObject + Database ScriptableObjectの対で構成されることが多い。

### Regions / Comments

- 調査した複数の大規模クラスでは `#region` が複数確認された。project全体を集計した規則ではなく、英語region (`Unity Lifecycle Methods`, `Damage & Death Logic`) と日本語/記号付きregion (`### UnityEvent用ラッパーメソッド ###`) が混在する。
- public APIや複雑な処理には日本語XML `<summary>` とinline commentが多い。段階番号付きコメントも見られる。
- 旧処理を大きなcomment blockで残す例がある (`PlayerManager`, `CharacterHealth`)。

### Architecture conventions

- Managerは `public static ... instance { get; private set; }` とAwake重複破棄を使う例が中心。
- `PersistentManagers.Awake()` は自身のGameObjectを永続化し、コードコメントでは子にglobal Managerを持つ親として説明されている。実際のPrefab子階層はC#だけでは確定しない。
- null guard、`?.`、warning/error logを多用する一方、null-forgiving (`!`) や非null前提の直接参照も存在する。
- 自作runtime codeはnamespaceなしが大半。例外として `CameraManager` / camera関連やCRIWAREコード等にnamespaceがある。

## Inferred

- 実際の採用方針は「Inspectorで調整しやすく、日本語コメントを厚くし、機能別regionで大規模MonoBehaviourを整理する」。
- 新規処理では既存singleton/event/ScriptableObject database方式へ合わせる傾向が強い。ただし設計規約が完全統一されているわけではない。
- 将来の新規コードでは、`AGENTS.md` に記載された方針としてbool名の `is` 接頭辞を原則推奨する。これは既存コードが完全に統一されているという事実認定とは分けて扱う。
- 将来の新規コードでは、既存箇所との整合を確認した上でPascalCase/camelCaseとregion構成を選ぶ。既存コードで確認した傾向を、例外のない必須規則として扱わない。

## Unityから呼ばれる可能性があるpublic API

### Confirmed

- 明示的UnityEvent wrapper: `FlagManager.SetPrologueTriggeredEvent`, `SetChapter1TriggeredEvent`, `SetTutorialEvent`, counted-event setters (`Assets/Scripts/Manager/FlagManager.cs`)。
- `FlagAction.ApplyFlagOperations()` はcommentでUnityEventからの呼出しを明記 (`Assets/Scripts/Flags/FlagAction.cs`)。
- Fungus callback明記: `LotteryGameManager.SetupGame`, `PayAndRevealEmpty`, `SkipReveal`, `OnChestSelected`; `FieldEvent_Chapter2.CheckVillageInquiryComplete()`。
- Animation Event relay: `SnowFieldGolemMediumAnimationEventRelay.OnSpearAttackAnimationEvent()` → `SnowFieldGolemMediumMoveController.OnSpearAttackAnimationEvent()`。
- Inspector event向け形状操作: `SpriteSizeController.SetWidth/SetHeight/SetSize/MultiplySize`。
- Fungus custom commandは `Assets/Scripts/FungusCustom/` の `Fungus.Command` 派生が `OnEnter()` をoverrideし、Flowchartから実行される。
- `TorchController`/`TorchGroupController`, `DirectionalObjectSpawner`, `WaypointMover`, `GridPushableBlock`, `MovingPlatformAudio` 等にもInspector eventへ登録可能なpublic actionがある。

### Inferred

- MonoBehaviourの引数なし/Unity対応引数を持つpublic methodは、C#参照がなくてもUnityEvent、Fungus Invoke Method、Animation Eventから呼ばれ得るため、未使用と断定できない。

## Editor / Test / Debug

### Confirmed

- `Assets/Editor/` にDatabase/Data custom inspector、CSV importer、Flag検索、Fungus command editor、scene/hierarchy補助、build date processor等がある。
- `Assets/Scripts/Developer/` にdebug audio、scene loader、camera follow、collision、weapon preview等がある。
- `Assets/Scripts/UIs/Debug/DebugMenuManager.cs`, `Assets/Scenes/DebugScene.unity`, `PlayerTestMoveController.cs`, `DebugTestScript.cs` が存在する。
- Test Framework packageはあるが、自作のEditMode/PlayMode test assemblyは発見できない。NaughtyAttributesのTest assemblyとFungus/LeanTweenのtest/sample codeは外部資産側。

## Unknown

- Editor上のserialization内容が命名規約どおりか、warningsが出ているか、Animation Eventのmethod名が有効か。
- comment-outされた旧コードを意図的な参考として維持する正式ルールの有無。
