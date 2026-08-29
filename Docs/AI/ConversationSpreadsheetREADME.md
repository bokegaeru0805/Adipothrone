# Adipothrone 会話制作スプレッドシート README

## 目的

Googleスプレッドシートで作成した会話データを、`UnityExport` タブからCSVとして手動ダウンロードし、Unity Editorの `SpreadsheetDialogueImporter` でFungus Flowchartへ同期する。

## 正式なスプレッドシート

- URL: https://docs.google.com/spreadsheets/d/1X-6Z7Apdl_33GOEJ8tn3Kg4J8B4rUEDfzJ7kVUvYHwA/edit
- 会話入力: `会話`
- フラグ条件入力: `条件`
- 使用可能フラグの登録: `フラグマスター`
- Unityへ出力するCSV: `UnityExport`
- 使い方: `使い方`
- キャラクター・体形・表情の参照: `設定`

## AIが編集する前の必須確認

1. 対象Block名が既存行にあるか検索する。
2. 既存行がある場合は、重複行を追加せず、ユーザーが指定した範囲だけを更新する。
3. 新規Blockの場合は、空き行へ行順を1から連番で追加する。
4. `会話` の数式列（E、J、K）を上書きしない。
5. `UnityExport` の数式を直接編集しない。出力は自動反映される。
6. 書き込み後、`会話` と `UnityExport` を再読込して内容を検証する。
7. Unityへの反映は自動ではない。`UnityExport` をCSVで手動ダウンロードする。

## `会話` 列定義（A:T）

| 列 | 項目 | 入力ルール |
|---|---|---|
| A | 入口Block名 | Unity FlowchartのBlock名。既存名と一致したBlockは同期時に再構築される。 |
| B | 分岐優先度 | 整数。大きい値の分岐から評価される。 |
| C | 行順 | 同じBlock・分岐内の再生順。通常は1から連番。 |
| D | Character名（表示名） | Fungusに表示する話者名。日本語や任意文字列を入力可能。 |
| E | Unity Character名（自動） | 数式列。直接編集禁止。 |
| F | 名前を表示 | TRUEならDを表示。FALSEならD、体形、表情を空欄扱いにする。 |
| G | セリフ | 1行につき1つのセリフ本文。 |
| H | 体形指定（複数時） | 複数候補のときだけ指定。通常は空欄。 |
| I | 表情指定（複数時） | 複数候補のときだけ指定。通常は空欄。 |
| J | 体形（確定） | 数式列。直接編集禁止。 |
| K | 表情（確定） | 数式列。直接編集禁止。 |
| L | 前文に続ける | 通常FALSE。前のSay表示へ続ける場合のみTRUE。 |
| M | クリック待ち | 通常TRUE。 |
| N | 終了時Fade | 通常TRUE。 |
| O | デフォルト | 条件なし分岐ではTRUE。条件付き分岐ではFALSE。 |
| P | メモ | 作成者向けメモ。 |
| Q | Command種別 | `Say`、`RandomSay`、`Choice` のいずれか。 |
| R | グループID | RandomSay候補をまとめるID。同じBlock・優先度・IDが候補集合になる。Sayでは空欄。 |
| S | 選択肢移動先Block | Choiceの移動先。SayとRandomSayでは空欄。 |
| T | ランダム候補ID | 同じIDの複数行を1つのRandomSay候補として順番に表示する。単独候補または通常会話では空欄。 |

## RandomSayの入力方法

- `Q=RandomSay`、同じ `R=グループID` を設定する。
- 候補を複数行で表示する場合、候補Aの行へ同じ `T=候補A`、候補Bの行へ同じ `T=候補B` を設定する。
- 各候補の行は `C` の行順で表示される。
- 同一候補内では話者名、表示名、Character、表情を統一する。
- `T` が空欄の行は、その1行だけで1候補になる。

## フラグ条件

- 条件は `条件` タブへ入力する。
- `条件` のBlock名と分岐優先度は `会話` と一致させる。
- 使用できるFlag IDは `フラグマスター` で「使用可」がTRUEのものだけ。
- Unity同期前にEnumType、FlagName、Enum数値が実際の `FlagData.cs` と検証される。
- 同一分岐内の複数条件は、指定した論理演算子で結合される。

## Unity同期

1. `UnityExport` タブをCSVとして手動ダウンロードする。
2. Unityの `SpreadsheetDialogueImporter` にCSVを設定する。
3. 「CSVを検証」を実行する。
4. エラーがないことを確認して「CSVからFungusを同期」を実行する。
5. CSVに記載されたBlockだけが再構築され、未記載Blockは変更されない。
6. 自動生成Blockには、先頭に `TalkStart`、末尾に `TalkEnd` が必ず追加される。

## AI編集時の出力報告

編集後は必ず次を報告する。

- 更新したシート名とセルまたは行
- 使用したBlock名
- 追加・更新した会話行数
- RandomSayの場合のグループIDと候補ID
- UnityExportへの反映確認結果
- CSVダウンロードとUnity同期を実行していない場合は、その旨

