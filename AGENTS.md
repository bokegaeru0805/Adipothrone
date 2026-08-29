# AGENTS.md

## プロジェクト概要

- プロジェクト名: Adipothrone -太りゆく世界-
- 種類: 2Dピクセルアート探索型アクションRPG
- Unity: 2022.3.24f1
- 主な言語: C#
- 主な外部技術・アセット: Fungus、Cinemachine 2.9.7、Universal Render Pipeline 14.0.10、DOTween、CRIWARE/ADX2、Easy Save 3、NaughtyAttributes

外部技術・アセットのversion、取得元、license、project側の改変範囲は、ファイルから確認できた範囲を超えて断定しない。詳細は `Docs/AI/ExternalAssets.md` を参照する。

## 詳細資料

実装・調査前に、作業範囲に応じて必要な資料だけを読む。AGENTS.mdには詳細なクラス一覧を重複掲載しない。

- `Docs/AI/ProjectInventory.md`: フォルダ、Package、Assembly Definition、Unity version
- `Docs/AI/Architecture.md`: システム構成、主要機能、データフロー
- `Docs/AI/ManagerMap.md`: Manager間のコード上の参照関係と参照方法
- `Docs/AI/CodingConventions.md`: 既存コードで確認された傾向と新規コードの推奨規則
- `Docs/AI/ExternalAssets.md`: 外部由来候補、根拠、変更時の注意
- `Docs/AI/Unknowns.md`: Unity Editor上または実行時に確認が必要な事項

## コンテキスト・使用量を抑えるための規則

Codexは、必要十分な範囲だけを調査・読取し、不要なプロジェクト全体探索や同じ内容の再読込を避ける。

### 基本方針

- 最初からプロジェクト全体を探索しない。
- まず変更対象ファイルと、その変更に直接関係するファイルだけを確認する。
- `Docs/AI/` の文書は現在の作業に関係するものだけを読む。毎回すべて読まない。
- 一度確認したファイルは、変更された場合や新しい疑問が生じた場合を除き、同じ作業中に繰り返し全文を読み直さない。
- ファイル全体を読む必要がない場合は、対象symbol周辺や必要な範囲だけを確認する。
- 検索結果が多い場合は、最初にファイル名・参照箇所だけを絞り込み、必要なファイルだけ内容を読む。
- 十分な根拠が得られた時点で調査を終了する。
- 追加調査を行っても結論が変わらないと判断できる場合は探索を続けない。
- ユーザーから明示されていない包括的レビュー、コード品質監査、無関係な改善点探索は行わない。
- 明示的な理由がない限り、作業対象と無関係なファイルを「念のため」に大量に読まない。
- 同じ検索を理由なく繰り返さない。

### 調査範囲の段階的拡大

調査は原則として次の順番で行う。

1. 変更対象ファイル
2. 同じ機能または同じフォルダにある直接関連ファイル
3. 変更対象symbolの参照元・参照先
4. 必要な場合のみ関連する `Docs/AI/`
5. 上記で判断できない場合のみ、より広い範囲またはプロジェクト全体

前の段階で十分に判断できる場合は、次の段階へ進まない。

### 軽微な変更

次をすべて満たす変更は、原則として「軽微な変更」として扱う。

- 原則1～数ファイル以内の変更
- privateな内部実装を中心とする
- public APIを変更しない
- `[SerializeField]` またはpublic fieldの既存の名前・型を変更しない
- SaveDataやセーブ互換性へ影響しない
- Managerのライフサイクルや永続化へ影響しない
- Prefab、Scene、Animator、Fungus、Timeline等との参照構造を変更しない
- 外部アセットを変更しない
- 複数の主要システムをまたぐ設計変更ではない

軽微な変更では、変更内容と直接関係しない以下の調査を省略してよい。

- プロジェクト全体の参照検索
- Prefab・Scene全体の検索
- 全Managerの関係調査
- 複数の類似実装の網羅的調査
- 関係のない `Docs/AI/` 文書の読取
- Animator、Fungus、Timeline等への無関係な参照調査
- 無関係なコード品質確認やリファクタリング候補の探索

ただし、調査中に影響範囲が想定より広いと判明した場合は、通常または高リスクな変更として必要な調査へ切り替える。

### 通常の変更

軽微な変更にも高リスクな変更にも該当しない場合は、変更内容に直接関係する範囲を調査する。

- 影響するクラス・API・参照関係を確認する。
- 必要な `Docs/AI/` のみ読む。
- 類似実装は原則として最も近い1～2件を確認する。
- 1～2件で判断できない場合のみ追加で調査する。
- Inspector、Prefab、Scene等の確認は、その変更がシリアライズやUnity側参照に関係する場合に行う。

### 高リスクな変更

次を含む場合は、既存の影響調査規則に従って十分に調査する。

- SaveDataまたはセーブ互換性
- `[SerializeField]`、public field、public APIの変更
- Managerの追加・削除・責務変更
- singletonまたは `DontDestroyOnLoad`
- 継承構造やinterfaceの変更
- Fungus、Animator、Animation Event、Timeline
- Prefab、Scene、ScriptableObjectの保存値
- 外部アセット
- 複数システムをまたぐ変更
- 原因不明の不具合調査
- 大規模なリファクタリング
- データ構造または保存形式の変更

### コード検索

- symbol検索は対象クラス名、method名、field名など具体的な検索語から開始する。
- 検索結果の全文を大量に読み込まず、まず該当ファイルと行を特定する。
- 明確な理由がない限り `Assets/` 全体を無差別に読み込まない。
- `Library/`、`Temp/`、`Logs/`、`obj/` 等は検索対象から除外する。
- 類似実装を探す場合は、最も近い候補から確認する。
- 必要な情報が得られた後も追加の類似実装を網羅的に確認し続けない。
- 同じ検索結果を、明確な理由なく再取得しない。

### ファイル読取

- ファイル全体が必要でない場合は、対象class、method、field周辺を優先して読む。
- 大きなファイルは、必要な箇所を先に検索してから読む。
- 一度内容を確認済みのファイルについて、同じ作業中に変更がない場合は、必要なく全文を読み直さない。
- `Docs/AI/` は作業に直接関係する資料のみ読む。
- 調査のためだけに無関係なScene、Prefab、Assetを大量に読み込まない。

### 実装後の確認

軽微な変更では、原則として次を優先する。

1. 変更対象ファイルを確認する。
2. `git diff` で意図した差分だけであることを確認する。
3. 変更に直接関係するコンパイルエラーまたは静的な問題を確認する。
4. Unity Editor上で必要な確認事項があれば簡潔に示す。

軽微な変更では、変更内容に関係しないシステムまで再調査しない。

通常または高リスクな変更では、「実装後の確認と報告」に定めた必要な確認を行う。

### 出力

- 調査途中の大量の検索結果やファイル全文を回答へ貼り付けない。
- ユーザーへの報告は、変更内容、重要な根拠、必要な確認事項を中心に簡潔にする。
- 問題がない項目を網羅的に列挙しない。
- 同じ説明を複数回繰り返さない。
- ユーザーが詳細な分析を求めていない場合は、必要以上に長い報告を行わない。

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

## 地上移動敵のResetState

- 地面を移動する敵のMoveControllerでは、落下やノックバック等で初期位置から移動した後でも、`ResetState()`によって適切な出現位置へ復帰できるようにする。
- 固定の初期位置へ戻す敵は、原則として`Awake()`時点の初期座標を保持し、`ResetState()`内でRigidbodyの速度・物理状態を初期化した後、移動範囲の計算やランダム配置より前に座標を復元する。
- Activator範囲内へランダム配置する敵は、初期の高さなど必要な座標を復元してから、移動範囲内の座標を決定する。
- `isUseManualInitialPosition`、相対Bound、外部Spawnerによる配置などがある場合は、それぞれの意図を維持し、無条件にワールド原点や現在位置を復帰先にしない。
- 飛行敵、追従敵、イベント配置敵など、初期位置への復帰が適切とは限らない敵へ一律適用しない。
- 実装後はUnity上で「足場から落下させる → 非アクティブ化または`ResetState()`を実行する → 想定した足場・高さへ復帰する」を確認する。
- 参考実装は `Assets/Scripts/Enemies/MoveController/SlimeWhiteMoveController.cs` と `Assets/Scripts/Enemies/MoveController/SnowFieldGolemMediumMoveController.cs`。

## 新規スクリプト作成前の調査

変更の規模とリスクに応じて必要な項目だけ確認する。軽微な新規component等では、無関係な調査を網羅的に行わない。

1. 関連する `Docs/AI/` 文書が必要な場合だけ読む。
2. 類似する既存実装を検索する。原則として最も近い1～2件を確認し、それで設計判断できない場合のみ追加で調査する。
3. 利用可能な基底クラス、interface、共通Utilityを確認する。
4. 必要に応じて呼び出し元、依存先、継承関係を確認する。
5. Inspector、Prefab、Scene、UnityEvent、Animation Event、Fungus、Timelineからの参照可能性は、そのスクリプトがそれらと関係する場合に確認する。
6. 新規ファイルと変更対象ファイルを事前に提示する。
7. 最小限の変更案を提示する。
8. ユーザーの実装許可を得てから変更する。

## 既存スクリプト改修前の影響調査

以下は、変更内容に影響する項目だけ確認する。

軽微な変更では必要な範囲に限定し、高リスクな変更では必要な項目を十分に確認する。

確認した場合は、必要に応じて根拠となる相対ファイルパスとクラス名を示す。

- 継承元、継承先、interface実装
- public APIのC#上の呼び出し元
- static `instance`参照、`GetComponent`参照、Inspector参照
- eventの発行元と購読先
- UnityEvent、Animation Event
- Fungus Custom Commandとincoming caller
- Timeline track、clip、bindingの可能性
- PrefabとSceneのシリアライズ参照

privateな内部処理のみの軽微な変更で、上記の仕組みに影響しないことが明らかな場合は、すべてを網羅的に調査する必要はない。

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

ただし、変更対象がこれらの仕組みに関係しないprivateな内部処理のみであることが明らかな場合は、毎回Fungus、Animator、Timelineを網羅的に調査しない。

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

未使用調査そのものが依頼内容に含まれていない場合は、変更対象外の旧実装・未使用候補を積極的に探索しない。

## コーディング規則

既存コードには命名・書式の反例や揺れがある。次は新規コードの推奨規則であり、既存コード全体が完全に準拠しているという事実認定ではない。

- bool名は原則として `is` から始める。
- enum値は明示的に設定する。
- Inspectorから設定しやすい構造にする。
- 既存コメントを理由なく削除しない。
- public APIには必要に応じてsummaryまたは用途コメントを付ける。
- privateな内部コンポーネント参照の名前は、原則として `_animator` のように先頭へアンダースコア `_` を付ける。
- 既存ファイルを変更するときは、周囲の書式と命名を優先する。
- 大きなクラスでは既存方針に合わせてregionを整理する。
- 数値差分だけを扱う目的で不要なScriptableObjectを追加しない。
- 不要なsingleton、Manager、抽象化、共通基底クラスを追加しない。
- 修正範囲外のコードをついでに整形・改名・一括置換しない。
- ユーザーの依頼を満たすために不要な新規クラスやUtilityを追加しない。
- 同じ目的を既存コードの小さな変更で実現できる場合は、過剰な抽象化を避ける。

## 作業モード

### 調査・設計

- 原則として既存ファイルを変更しない。
- 問題を発見しても勝手に修正しない。
- Confirmed、Inferred、Unknownを区別する。
- 根拠となる相対ファイルパスとクラス名を示す。
- 必要十分な根拠が得られた時点で調査を終了する。
- ユーザーが要求していないプロジェクト全体レビューへ拡大しない。

### 実装

- ユーザーが実装を明示的に許可した場合だけ変更する。
- 事前に変更対象ファイルを示す。
- 最小限の差分にし、関係のないリファクタリングを行わない。
- Unity Assetや外部ファイルの変更は、許可された範囲を厳守する。
- 実装中に新たな問題を発見しても、依頼範囲外なら原則として勝手に修正しない。
- 変更対象が明確な場合は、実装のためだけに不要な広範囲探索を行わない。

### レビュー

- ユーザーがレビューのみを求めている場合はファイルを変更しない。
- 問題を重要度順に報告し、修正案と根拠を示す。
- Unity Editor上の確認が必要な事項を、コードから確認できる問題と分ける。
- レビュー対象として指定された範囲を優先し、依頼がない限りプロジェクト全体レビューへ拡大しない。

## 変更禁止範囲

明示的な依頼がない限り、次を変更しない。自動生成・一時フォルダは原則として調査対象からも除外する。

- `Library/`, `Temp/`, `Logs/`, `obj/`
- `Build/`, `Builds/`, `MemoryCaptures/`
- `.vs/`, `.idea/`
- 外部アセットの元ファイル
- Scene、Prefab、Animator Controller、Animation Clip
- Package設定、`ProjectSettings/`, `UserSettings/`

## 実装後の確認と報告

変更の規模とリスクに応じて必要な項目を確認する。すべての変更で以下を機械的に網羅する必要はない。

### 軽微な変更

原則として次を確認する。

1. 変更したファイル一覧を示す。
2. `git diff`を確認する。
3. 意図しないファイルやコードが変更されていないことを確認する。
4. 可能な範囲で、変更に直接関係するコンパイルエラーを確認する。
5. Unity Editor上で必要な手動確認がある場合は示す。

### 通常・高リスクな変更

必要に応じて次を確認する。

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

変更内容に無関係なシステムを、実装後の確認という理由だけで再度広範囲に調査しない。

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

これらは、その事項が現在の変更に関係する場合に確認する。現在の作業と無関係な未確認事項を、毎回すべて調査する必要はない。
