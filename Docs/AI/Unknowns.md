# Unknowns and Unity Editor Checklist

この文書の項目は、C#・設定ファイルだけでは確定できないか、今回の静的調査で未解決のもの。

## 最優先でEditor確認する事項

### Unknown

1. `Assets/Prefabs/Global/PersistentManagers.prefab` の子構成と、Game/Save/Flag/Item/Weapon/Skill/Input/Audio等の各Managerが実際に同じ永続root配下か。
2. 各起動Scene (`TitleScene`, `TutorialStartScene` 等) における `PersistentManagers`, `GlobalFlowchartController`, `FadeCanvas`, `ObjectPooler` の配置と重複生成の有無。
3. Project SettingsのScript Execution Order。ManagerのAwake/Start依存が安全な順序か。
4. `GameManager` の各Database、`DropItemPrefab`、GlobalFlowchartとblock名 `Treasurebox` / `SkillGet` の割当。
5. `PlayerManager` のPlayer prefab、各Database、FastTravel/StatusLevel参照と、sceneごとの配置整合性。
6. Main CameraのCinemachine Brain/Virtual Camera/Confiner2D、CameraManager、CameraMoveAreaのbinding。

## Inspector / Prefab / Scene

### Unknown

- 全 `[SerializeField]` / public inspector fieldが割当済みか。特にManager、health、enemy move controller、UI refsは項目数が多い。
- Scene/Prefab override、missing script、missing reference、broken prefab、重複EventSystemの有無。
- `EnemyData`, weapon/item/database assetのID重複、null entry、表示順、価格/能力値の整合性。
- tag/layer/sorting layer/physics matrixが `GameConstants` とcontrollerの期待に一致するか。
- Build Settingsのscene順と、debug/demo sceneが製品buildに含まれるか。
- Addressables/Resources/StreamingAssetsの実運用範囲。

## UnityEvent / Fungus / Animation Event

### Unknown

- `FlagAction`, `FlagDrivenState`, `GimmickSwitch`, `TargetGroupObserver`, torch/gimmick/visual classesのUnityEvent listener一覧。
- Fungus Flowchart内のblock名、custom command、public callbackの実使用。特に `LotteryGameManager` と `FieldEvent_Chapter2.CheckVillageInquiryComplete`。
- Animation Clipに登録されたevent method一覧。静的に明示確認できたrelayは `SnowFieldGolemMediumAnimationEventRelay.OnSpearAttackAnimationEvent` のみ。
- Timeline asset上でcustom track (`BGM`, `SE`, `Fade`, `Warp`, `CameraMove`, `CameraShake`, `BoolFlag`, `Heroine`) がどうbindingされるか。
- public methodがC#参照なしでもInspector経由で利用されるため、未使用判定はEditor側listenerとAnimation Eventの確認後に行う必要がある。

## 外部資産・sample・旧実装

### Unknown

- `ZZ_UnusedScripts` の各MonoBehaviourがGUID参照されていないか。
- `Developer/`, `DebugScene`, `Demo_AllIn1SpriteShader`, 外部Demo/Testがbuild対象か。
- CRIWARE cue sheet、ACB/AWB、platform別設定の有効性とlicense状態。
- Easy Save 3 generated type (`Assets/Easy Save 3/Types/ES3UserType_GameManager.cs`) が現行SaveData構造と一致するか。
- Fungus、NaughtyAttributes、TrueShadow、AllIn1SpriteShader、Shapes2D、VFX素材の正確なversion・license・ローカル改変。
- Effekseerが導入済みか。manifest、自作C#のnamespace、主要フォルダ名からは直接確認できなかった。

## 実行・品質

### Unknown

- Console上のcompile warning/error、obsolete API、serialization warning。
- Title→New/Load→各章→Save/Load→Death/FastTravel→Titleの実行時lifecycleとsingleton残留。
- ObjectPoolerのPersistent/Scene pool返却、scene unload時のreset、敵の `IEnemyResettable.ResetState()` の実動作。
- 自作automated testsは発見できなかったため、主要フローのregression coverage。
- WebGL/Windows等のtarget別にCRIWARE、save path、input、URPが正常動作するか。

## Inferred risks（修正は未実施）

- singletonが多く、初期化順とscene配置に依存するため、Editor hierarchyと実行時DontDestroyOnLoad sceneの確認が重要。
- `GameManager.savedata` の置換後に複数Manager/UIが保持するcacheを再同期する必要があり、`SaveLoadManager` が全利用者を網羅しているか実行確認が必要。
- spelling/casingの揺れは既存asset pathやserialized referenceと結び付くため、名称整理を行う場合も単純renameは避ける必要がある。
