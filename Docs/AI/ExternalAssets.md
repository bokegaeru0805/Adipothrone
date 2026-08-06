# External Assets

## Confirmed

| 外部要素または候補 | 根拠 | Confirmedの内容 |
|---|---|---|
| Fungus | `Assets/Fungus/Fungus.asmdef`, `Assets/Editor/SayEditor.cs` | Fungus名のassemblyと `Fungus.EditorUtils` namespaceが存在する |
| Cinemachine 2.9.7 | `Packages/manifest.json` | package名とversionが記載され、`CameraManager`等に `using Cinemachine` がある |
| URP 14.0.10 | `Packages/manifest.json` | package名とversionが記載されている |
| DOTween | `Assets/Scripts/Manager/GameManager.cs`, `Assets/Scripts/FadeCanvas.cs`, `Assets/Scripts/FastTravelPoint.cs` | `using DG.Tweening` とDOTween API呼出しがある |
| CRIWARE / ADX2 | `Assets/CRIMW/` 内のasmdef、`Assets/Scripts/Manager/BGMManager.cs`, `SEManager.cs` | `CriMw.CriWare.*` assemblyとCRIWARE API参照がある |
| Easy Save 3 | `Assets/Scripts/Manager/SaveLoadManager.cs`, `Assets/Easy Save 3/Types/ES3UserType_GameManager.cs` | ES3 API呼出しとES3 user typeファイルがある |
| NaughtyAttributes | `Assets/NaughtyAttributes/` 内のasmdef | Core、Editor、Testのassembly定義がある |
| TrueShadow | `Assets/Plugins/TrueShadow/` 内のasmdef | Runtime、Editor、Demoのassembly定義がある |
| AllIn1SpriteShader | `Assets/Plugins/AllIn1SpriteShader/AllIn1SpriteShaderAssembly.asmdef`, `Assets/Scenes/Demo_AllIn1SpriteShader.unity` | asmdefとSceneファイルが存在する |
| Shapes2D | `Assets/Shapes2D/` | C#、shader、resourceファイルが存在する |
| Hovl Studio Magic effects pack | `Assets/Hovl Studio/Magic effects pack/` | prefab、texture、materialファイルが存在する |
| Eric VFX Studio | `Assets/Eric VFX Studio/` | フォルダが存在する |
| Piloto Studio | `Assets/Piloto Studio/` | フォルダが存在する |

### Packageとして確認したその他

- Unity 2D Animation/Aseprite/PSD Importer、TextMeshPro、UGUI、Timeline、Visual Scripting、Post Processing、Recorder、Memory Profiler、Test Framework。根拠: `Packages/manifest.json`。

## 外部由来を直接確認できる根拠

### Confirmed

- `Assets/Fungus/Fungus.asmdef` と `Assets/Fungus/Scripts/Editor/FungusEditor.asmdef` はFungus assemblyを定義する。
- `Assets/CRIMW/` 内の複数asmdefは `CriMw.CriWare.*` のassembly名とnamespaceを持つ。
- `Assets/NaughtyAttributes/` にはCore、Editor、Testのasmdefがある。
- `Assets/Plugins/TrueShadow/` にはRuntime、Editor、Demoのasmdefがある。
- `Assets/Plugins/AllIn1SpriteShader/AllIn1SpriteShaderAssembly.asmdef` が存在する。
- `Assets/Editor/SayEditor.cs` は `Fungus.EditorUtils` namespaceを使用し、ファイル内コメントでFungusライブラリの一部であることを記載している。
- `Packages/manifest.json` にCinemachine、URP等のUnity package名とversionが記載されている。

### Inferred

- `Assets/Scripts/`, `Assets/Editor/`, game-specific `*Data/`, `Prefabs/`, `Scenes/` はproject固有ファイルを多く含む構成とみられるが、外部由来・外部コードの改変・sampleが混在する可能性があるため、フォルダ単位では自作と断定できない。
- `Assets/Scenes/Demo_AllIn1SpriteShader.unity` はファイル名と対応pluginから外部demo候補とみられる。
- TrueShadowはassembly名とフォルダ名からshadow機能を提供するplugin候補、Hovl Studio Magic effects packはフォルダ名と格納ファイルからVFX素材候補とみられるが、用途の確定にはREADME等の直接的な説明が必要。
- NaughtyAttributes `Scripts/Test/`, TrueShadow `Demo/`, Fungus thirdparty codeは、asmdef名、フォルダ名、namespaceから外部asset側のtest/demo/thirdparty候補とみられる。
- `Assets/Eric VFX Studio/` と `Assets/Piloto Studio/` の名称からstudio提供物である可能性はあるが、用途と由来はファイルの存在だけでは確定しない。

### Unknown

- 各外部候補のlicense header、README、購入・取得元、project側での改変範囲。根拠ファイルを確認できないものは外部資産と断定できない。
- `Assets/Eric VFX Studio/` と `Assets/Piloto Studio/` の具体的用途、由来、license。

## 旧実装・重複・未使用候補

以下は削除可否を示さず、名前・配置・commentから得た候補のみ。

### Confirmed

- `Assets/Scripts/ZZ_UnusedScripts/` に14本のC#ファイルが配置されている。例: `TimelineSkipManager_UnUsed`, `SceneLoader`, `BaseItemManager`, `MovingPlatform`。
- `Robot_wave_move.cs` はclass宣言がcomment-outされている。
- audio debug実装が `Developer/DebugBGMManager`, `DebugSEManager`, `Dev_webGLCriBgmPlayer` 等に複数ある。
- `TimelineSkipManager_UnUsed.cs` と現行 `Manager/TimelineSkipManager.cs`、旧moving platform群と現行 `Objetcs/Platform/MovingPlatformAudio.cs` 等、役割が重なる名前がある。
- `CharacterHealth` と `PlayerManager` に旧処理の大きなcomment blockが残る。
- `Assets/Editor/ContactDamageController.cs` はruntime側 `Assets/Scripts/Enemies/ContactDamageController.cs` と同名だが、前者の内容/型の役割はEditor scriptとして別途確認が必要。

### Inferred

- `ZZ_UnusedScripts` はフォルダ名から旧実装または退避目的の候補とみられるが、配置意図と使用状況は未確認。`Developer` と `DebugScene` は製品runtime外の開発支援候補とみられる。
- 外部assetのDemo/Testは製品ロジックではない可能性が高い。

### Unknown

- `.meta` GUIDを通じたScene/Prefab/ScriptableObject参照を全件解決していないため、どの候補も「未使用」とは確定できない。
- `ZZ_UnusedScripts` の14本が実際に未使用か、また退避目的で配置されたか。
- 各外部assetの購入版version、license、更新元、改変有無。DOTweenの格納場所/正確なversion。
- Effekseerの明示的コード/フォルダは今回の対象検索では確認できず、導入有無は未確定。
