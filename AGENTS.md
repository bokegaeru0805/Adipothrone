# AGENTS.md

## プロジェクト概要

- プロジェクト名: Adipothrone -太りゆく世界-
- 種類: 2Dピクセルアート探索型アクションRPG
- Unity: 2022.3.24f1
- 主な言語: C#
- 主な外部技術・アセット: Fungus、Cinemachine 2.9.7、Universal Render Pipeline 14.0.10、DOTween、CRIWARE/ADX2、Easy Save 3、NaughtyAttributes

外部技術・アセットのversion、取得元、license、project側の改変範囲は、ファイルから確認できた範囲を超えて断定しない。詳細は `Docs/AI/ExternalAssets.md` を参照する。

## 詳細資料

実装・調査前に、作業範囲に応じて次を読む。AGENTS.mdには詳細なクラス一覧を重複掲載しない。

- `Docs/AI/ProjectInventory.md`: フォルダ、Package、Assembly Definition、Unity version
- `Docs/AI/Architecture.md`: システム構成、主要機能、データフロー
- `Docs/AI/ManagerMap.md`: Manager間のコード上の参照関係と参照方法
- `Docs/AI/CodingConventions.md`: 既存コードで確認された傾向と新規コードの推奨規則
- `Docs/AI/ExternalAssets.md`: 外部由来候補、根拠、変更時の注意
- `Docs/AI/Unknowns.md`: Unity Editor上または実行時に確認が必要な事項

## アーキテクチャ上の重要事項

- `GameManager.savedata` は現在の `SaveData` を保持し、複数のManagerから参照される。
- `PersistentManagers.Awake()` は自身のGameObjectに対して `DontDestroyOnLoad(gameObject)` を呼ぶ。コードコメントでは、子にglobal Managerを持つ親オブジェクトとして説明されている。
- `PersistentManagers.prefab` の実際の子階層と各Sceneでの配置は未確認であり、Unity Editorで確認する。
- `PlayerManager` はコードコメント上scene-localとして扱われている。安易に `DontDestroyOnLoad` を追加しない。
- GameManagerへの登録・参照と、GameObjectの永続化は別の仕組みとして扱う。
- 依存関係を調査するときは、static `instance`参照、`GetComponent`、Inspector参照、event購読、method call、incoming callerを区別する。コード上の参照関係からGameObjectの親子・所有関係を断定しない。

## 新規Manager追加時の規則

1. 類似Managerの責務、配置候補、参照方法を調査する。
2. 永続ManagerかScene固有Managerかを判断する。
3. `PersistentManagers`配下への追加を想定する場合も、実Prefab階層をUnity Editorで確認する。
4. 新しいsingletonや `DontDestroyOnLoad` を安易に追加しない。
5. 既存Managerの責務や既存componentで対応できないか確認する。
6. 他componentからの登録・取得方法と、GameObjectの永続化方法を別々に検討する。

## 新規スクリプト作成前の調査

1. 関連する `Docs/AI/` 文書を読む。
2. 類似する既存実装を複数検索する。
3. 利用可能な基底クラス、interface、共通Utilityを確認する。
4. 呼び出し元、依存先、継承関係を確認する。
5. Inspector、Prefab、Scene、UnityEvent、Animation Event、Fungus、Timelineからの参照可能性を確認する。
6. 新規ファイルと変更対象ファイルを事前に提示する。
7. 最小限の変更案を提示する。
8. ユーザーの実装許可を得てから変更する。

## 既存スクリプト改修前の影響調査

次を確認し、根拠となる相対ファイルパスとクラス名を示す。

- 継承元、継承先、interface実装
- public APIのC#上の呼び出し元
- static `instance`参照、`GetComponent`参照、Inspector参照
- eventの発行元と購読先
- UnityEvent、Animation Event
- Fungus Custom Commandとincoming caller
- Timeline track、clip、bindingの可能性
- PrefabとSceneのシリアライズ参照

C#上で参照が見つからないことだけを理由に、publicメソッドやフィールドを未使用と判断しない。Inspector、UnityEvent、Animation Event、Fungus、Animator、Timeline、Prefab、Sceneから呼ばれる可能性を考慮する。

## Unityシリアライズに関する規則

- `[SerializeField]`およびpublic fieldの名前を安易に変更・削除しない。
- 改名時は `FormerlySerializedAs` の必要性を検討する。
- 型変更、削除、配列・List構造の変更がPrefab、Scene、ScriptableObjectの保存値へ与える影響を確認する。
- Inspector上の既存値を失う可能性があれば事前に報告する。
- Scene、Prefab、Animator Controller、Animation Clip等のYAMLは原則として直接編集しない。
- Unity Editor操作が必要な場合は、変更ではなく具体的な設定手順として報告する。
- 大量のAsset変更が必要な場合は直接YAMLを一括編集せず、ユーザーの許可を得た上でEditor拡張による処理を検討する。

## SaveDataに関する規則

- `GameManager.savedata` の置換・読み込み後に、SaveDataまたは派生情報をキャッシュするManagerやUIの再同期が必要か確認する。
- Weapon、Flag、PlayerEffect、Player、UI等の関連処理と、`SaveLoadManager`による反映処理を調査する。
- 古いSaveDataに新しいfieldが存在しない場合の初期値と互換性を考慮する。
- セーブ形式に影響する変更を通常の内部リファクタリングと同様に扱わない。
- 保存済みデータへの影響、移行の必要性、後方互換性のリスクを事前に報告する。
- コードまたは実行結果で確認できない同期処理を、存在するものとして断定しない。

## Fungus・Animator・Timelineに関する規則

- publicメソッドはFungus、Animation Event、UnityEvent、Timelineから呼ばれる可能性がある。
- Fungus Custom Commandから対象クラスへのincoming callerを確認する。
- Flowchartのblock名、変数、command順、callbackの実使用はUnity Editor上の確認事項とする。
- Animation Eventから呼ばれるメソッド名の変更・削除は、Clip上の登録を確認してから行う。
- Animator parameter名、State名、Animation Event、Timeline bindingをコードだけから断定しない。
- Unity Editor上で確認できていない内容はUnknownとして報告する。

## 外部アセットに関する規則

- フォルダ名だけで外部アセット・自作コードを断定しない。
- namespace、asmdef、license header、README、package情報、ファイル内コメント等をファイル単位で確認する。
- `Assets/`内には外部由来コード、改変コード、自作コードが混在し得る。
- 外部アセットの元ファイルは、明示的な依頼なしに変更しない。
- 変更が必要な場合は、自作ラッパー、継承、拡張クラス、project側の別フォルダへの追加を優先して検討する。
- version、取得元、license、改変範囲が未確認ならUnknownとして扱う。

## 旧実装・未使用候補に関する規則

- `ZZ_UnusedScripts`、`Developer`、Demo、Test等の名称だけで削除対象と判断しない。
- `.meta` GUID、Prefab、Scene、UnityEvent、Fungus、Animator、Animation Event、Timelineからの参照可能性を確認する。
- 未使用であることを断定せず、未使用候補または旧実装候補として報告する。
- 削除や移動は影響範囲を示し、明示的な許可を得てから行う。

## コーディング規則

既存コードには命名・書式の反例や揺れがある。次は新規コードの推奨規則であり、既存コード全体が完全に準拠しているという事実認定ではない。

- bool名は原則として `is` から始める。
- enum値は明示的に設定する。
- Inspectorから設定しやすい構造にする。
- 既存コメントを理由なく削除しない。
- public APIには必要に応じてsummaryまたは用途コメントを付ける。
- 既存ファイルを変更するときは、周囲の書式と命名を優先する。
- 大きなクラスでは既存方針に合わせてregionを整理する。
- 数値差分だけを扱う目的で不要なScriptableObjectを追加しない。
- 不要なsingleton、Manager、抽象化、共通基底クラスを追加しない。
- 修正範囲外のコードをついでに整形・改名・一括置換しない。

## 作業モード

### 調査・設計

- 原則として既存ファイルを変更しない。
- 問題を発見しても勝手に修正しない。
- Confirmed、Inferred、Unknownを区別する。
- 根拠となる相対ファイルパスとクラス名を示す。

### 実装

- ユーザーが実装を明示的に許可した場合だけ変更する。
- 事前に変更対象ファイルを示す。
- 最小限の差分にし、関係のないリファクタリングを行わない。
- Unity Assetや外部ファイルの変更は、許可された範囲を厳守する。

### レビュー

- ユーザーがレビューのみを求めている場合はファイルを変更しない。
- 問題を重要度順に報告し、修正案と根拠を示す。
- Unity Editor上の確認が必要な事項を、コードから確認できる問題と分ける。

## 変更禁止範囲

明示的な依頼がない限り、次を変更しない。自動生成・一時フォルダは原則として調査対象からも除外する。

- `Library/`, `Temp/`, `Logs/`, `obj/`
- `Build/`, `Builds/`, `MemoryCaptures/`
- `.vs/`, `.idea/`
- 外部アセットの元ファイル
- Scene、Prefab、Animator Controller、Animation Clip
- Package設定、`ProjectSettings/`, `UserSettings/`

## 実装後の確認と報告

1. 変更したファイル一覧を示す。
2. 各変更の目的を説明する。
3. 既存API、SerializeField、保存済みデータへの影響を報告する。
4. Inspector、Prefab、Animator、Fungus等で必要な設定を示す。
5. Unity上で行う手動テストを示す。
6. 実行できたテストと実行できなかったテストを分ける。
7. 可能な範囲でコンパイルエラーを確認する。
8. `git diff`を確認する。
9. 関係のないファイルが変更されていないことを確認する。
10. 残っているUnknown、リスク、未確認事項を報告する。

Unity Editorを実際に操作・実行できていない場合は「動作確認済み」と書かない。

## Unity Editor上で未確認の事項

次はUnity Editorまたは実行環境で確認する。詳細は `Docs/AI/Unknowns.md` を参照する。

- `PersistentManagers`, `LocalManagers`, `UIManagers`のPrefab階層
- SceneごとのManager配置と重複
- Player PrefabのComponent構成
- Inspector参照、UnityEvent listener、Fungus Flowchart
- Animator parameter、Animation Event、Timeline binding
- Script Execution Order、Build Settings
- missing script、missing reference、Prefab override
- 外部資産のlicense、version、取得元、改変範囲
- 旧実装・未使用候補の実使用状況
