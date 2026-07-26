# Project Inventory

調査日: 2026-07-27  
調査範囲: `Library/`, `Temp/`, `Logs/`, `obj/`, `Build/`, `Builds/`, `MemoryCaptures/`, `.vs/` を除く、プロジェクト内の既存ファイル。  
分類: **Confirmed** はファイルから直接確認、**Inferred** は複数の実装からの推測、**Unknown** は Unity Editor での確認が必要な事項。

## 1. Unity バージョン

### Confirmed

- Unity Editor は **2022.3.24f1**、revision は `334eb2a0b267`。根拠: `ProjectSettings/ProjectVersion.txt`。
- Product Name は `Adipothrone`、Company Name は `DefaultCompany`。根拠: `ProjectSettings/ProjectSettings.asset`。
- `Assets/`, `Packages/`, `ProjectSettings/` が揃っているため、このワークスペースは Unity プロジェクトルートである。

## 2. Packages

### Confirmed

`Packages/manifest.json` で直接宣言されている主なパッケージ:

| 領域 | package | version |
|---|---|---:|
| Render | `com.unity.render-pipelines.universal` | 14.0.10 |
| Camera | `com.unity.cinemachine` | 2.9.7 |
| 2D | `com.unity.feature.2d` | 2.0.0 |
| 2D Animation | `com.unity.2d.animation` | 9.1.3 |
| Aseprite | `com.unity.2d.aseprite` | 1.1.11 |
| PSD Importer | `com.unity.2d.psdimporter` | 8.0.5 |
| UI | `com.unity.ugui` | 1.0.0 |
| Text | `com.unity.textmeshpro` | 3.0.6 |
| Timeline | `com.unity.timeline` | 1.7.6 |
| Visual Scripting | `com.unity.visualscripting` | 1.9.2 |
| Test | `com.unity.test-framework` | 1.1.33 |
| Memory Profiler | `com.unity.memoryprofiler` | 1.1.9 |
| Recorder | `com.unity.recorder` | 4.0.3 |

完全な推移的依存関係は `Packages/packages-lock.json` にある。

## 3. Assets の主要構成

### Confirmed

| フォルダ | 主な内容 |
|---|---|
| `Assets/Scripts/` | Manager、Player、Enemy、UI、Data、SaveData、Fungus拡張、Timeline等のC#ファイル。フォルダ全体の由来はこの表では分類しない |
| `Assets/Editor/` | CustomEditor、Importer、設定画面、デバッグ補助のC#ファイル |
| `Assets/Scenes/` | `TitleScene`, `TutorialStartScene`, `Chapter1Scene`, `DesertScene`, `SnowScene`, `RoyalCapitalScene`, `DebugScene`, `Demo_AllIn1SpriteShader` のSceneファイル |
| `Assets/Prefabs/` | Global/Manager、Player、Enemy、UI、Object、Zone、Effect、Decoration のPrefab |
| `Assets/*Data/`, `Assets/Database/` | Enemy、Weapon、Item、Skill、Shop、Treasure、FastTravel、Tips等のScriptableObjectデータ |
| `Assets/Animations/`, `Atlases/`, `Sprites/`, `TilePalette/` | 2D表示・アニメーション・タイル関連 |
| `Assets/Effects/`, `Material/`, `Shaders/`, `Gloval Volume/` | VFX、Material、Shader、URP Volume Profile |
| `Assets/SoundData/`, `CriData/`, `StreamingAssets/` | CRIWAREを含む音声データ |
| `Assets/Fungus/`, `CRIMW/`, `Easy Save 3/`, `NaughtyAttributes/` | フォルダが存在する。C#、asmdef、Editor、Test、保存用type等のファイルを含む |
| `Assets/Plugins/`, `Shapes2D/`, `Hovl Studio/`, `Eric VFX Studio/`, `Piloto Studio/` | フォルダが存在する。C#、asmdef、shader、material、texture、prefab等、フォルダごとに異なるファイルを含む |

### Inferred

- `Assets/Prefabs/Global/PersistentManagers.prefab` が永続Managerの構成元、`LocalManagers.prefab` と `UIManagers.prefab` がシーン単位Managerの構成元とみられる。C#上の実配置は確定しない。
- `Assets/Scenes/Demo_AllIn1SpriteShader.unity` はファイル名と対応プラグインフォルダから外部サンプルシーンとみられる。
- `Assets/Editor/` にはproject固有のEditor拡張と外部由来または改変されたEditorコードが混在する可能性がある。例えば `Assets/Editor/SayEditor.cs` はファイル内コメントとnamespaceからFungusとの関係を直接確認できるが、フォルダ全体の由来は断定できない。
- `Assets/Fungus/`, `CRIMW/`, `Easy Save 3/`, `NaughtyAttributes/` は、asmdef名、namespace、ファイル内コメント等から外部由来のファイルを含むと判断できるが、各フォルダ全体の由来やproject側の改変範囲は確定しない。
- `Assets/Plugins/`, `Shapes2D/` およびstudio名を持つフォルダは、名称と格納ファイルからplugin・素材を含む候補とみられるが、フォルダ全体の由来は確定しない。

## 4. Assembly Definition

### Confirmed

- `Assets/Scripts/` と `Assets/Editor/` の直下・配下にはproject固有コードを分離する `.asmdef` がない。asmdef境界外のruntime/editor codeは既定の `Assembly-CSharp` / `Assembly-CSharp-Editor` に入る構成である。
- 発見した `.asmdef` は次のasset/plugin候補フォルダ内にある:
  - Fungus: `Assets/Fungus/Fungus.asmdef`, `Assets/Fungus/Scripts/Editor/FungusEditor.asmdef` ほか。
  - CRIWARE: `Assets/CRIMW/CriWare/Runtime/CriMw.CriWare.Runtime.asmdef`, Editor、Assets、Addressables用の複数assembly。
  - NaughtyAttributes: Core、Editor、Testの3assembly。
  - TrueShadow: Runtime、Editor、Demoの3assembly。
  - AllIn1SpriteShader: `Assets/Plugins/AllIn1SpriteShader/AllIn1SpriteShaderAssembly.asmdef`。
- `Fungus.asmdef` は `autoReferenced: true`。CRIWARE Runtimeは Timeline と Profiling Core を参照し、対応するversion defineを持つ。

### Inferred

- 自作コード全体が大きな単一runtime assemblyに集約され、機能境界はassemblyではなくフォルダとクラス規約で表現されている。

### Unknown

- Unityコンパイル結果、プラットフォーム別define、assembly間の実際のコンパイルエラー有無はEditor起動が必要。
- asset/plugin候補フォルダの取得元、license、project側での改変範囲。
