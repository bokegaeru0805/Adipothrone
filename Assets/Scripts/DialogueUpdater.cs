using System.Collections.Generic;
using System.IO;
using System.Linq;
using Fungus;
using UnityEditor;
using UnityEngine;
using System.Text.RegularExpressions;

/// <summary>
/// CSVの1行分のデータを格納するためのクラス。
/// </summary>
[System.Serializable]
public class DialogueLineData
{
    public string character;
    public string expression;
    public string dialogue;
}

/// <summary>
/// CSVデータをもとにFungusのFlowchartを更新するためのメインクラス。
/// </summary>
public class DialogueUpdater : MonoBehaviour
{
    [Header("基本設定")]
    public Flowchart targetFlowchart;
    public List<TextAsset> csvFiles = new List<TextAsset>();

    // CSVファイルの各列がどのデータに対応するかのインデックス（0から始まる番号）
    private const int COL_DIALOGUE = 0; //セリフの列
    private const int COL_CHARACTER = 1; //キャラクター名の列
    private const int COL_BLOCK_NAME = 2; //ブロック名の列
    private const int COL_EXPRESSION = 3; //表情の列

    // 「地の文」として扱うキーワード
    private const string NARRATIVE_TEXT_KEYWORD = "narrative";
    //　「ヒロイン」として扱うキーワード
    private const string HEROIN_KEYWORD = "Heroin";


    // [ContextMenu("Update Dialogue Sequentially by BlockName")]
    public void UpdateDialogue()
    {
        if (targetFlowchart == null || csvFiles.Count == 0)
        {
            Debug.LogError("FlowchartまたはCSVファイルが指定されていません。");
            return;
        }

        // --- Step 1: 全CSVを読み込み、BlockNameごとにセリフのリストを作成 ---
        var dialogueByBlock = new Dictionary<string, List<DialogueLineData>>();

        foreach (var csvFile in csvFiles)
        {
            if (csvFile == null)
                continue;

            StringReader reader = new StringReader(csvFile.text);
            reader.ReadLine(); // ヘッダーを読み飛ばす

            int lineNumber = 1;
            while (reader.Peek() != -1)
            {
                lineNumber++;
                string line = reader.ReadLine();
                string[] values; // values変数をifブロックの外で宣言

                // 1. この行が<sprite>タグを含んでいるかチェック
                //    CSVの仕様上、タグを含むセルは " で囲まれることが多いため、"<sprite" でチェック
                if (line.Contains("\"<sprite"))
                {
                    // 2. タグを含む行の場合：正規表現を使って、""で囲まれた中のカンマを無視して分割
                    values = Regex.Split(line, ",(?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)");
                }
                else
                {
                    // 3. タグを含まない通常の行の場合：シンプルなカンマ区切りで分割
                    values = line.Split(',');
                }

                // 定数を使って必要な最大の列インデックスを計算
                int maxIndex = new[]
                {
                    COL_BLOCK_NAME,
                    COL_CHARACTER,
                    COL_EXPRESSION,
                    COL_DIALOGUE,
                }.Max();

                if (values.Length <= maxIndex)
                {
                    Debug.LogWarning(
                        $"CSV Warning: {csvFile.name} の {lineNumber}行目の列数が不足しています。スキップします。"
                    );
                    continue;
                }

                // 4. 取得した各値から、前後の空白と余分な " を取り除く
                // 定数を使って各列のデータを取得
                string blockName = SanitizeCsvField(values[COL_BLOCK_NAME]);
                string characterName = SanitizeCsvField(values[COL_CHARACTER]);
                string expressionName = SanitizeCsvField(values[COL_EXPRESSION]);
                string dialogueText = SanitizeCsvField(values[COL_DIALOGUE]);

                if (string.IsNullOrEmpty(blockName) || string.IsNullOrEmpty(characterName))
                    continue;

                // --- 特別ルールの適用 ---

                // 定数を使ってキーワードを比較
                if (characterName == NARRATIVE_TEXT_KEYWORD)
                {
                    characterName = "";
                    expressionName = "";
                }

                var lineData = new DialogueLineData
                {
                    character = characterName,
                    expression = expressionName,
                    dialogue = dialogueText,
                };

                if (!dialogueByBlock.ContainsKey(blockName))
                {
                    dialogueByBlock.Add(blockName, new List<DialogueLineData>());
                }
                dialogueByBlock[blockName].Add(lineData);
            }
        }

        // --- Step 2: Flowchartを走査し、各BlockのSayコマンドを更新 ---
        int totalUpdatedCount = 0;
        bool hasChanged = false;

        // Flowchart内の全Blockをループ
        foreach (Block block in targetFlowchart.GetComponents<Block>())
        {
            // CSVデータの中に、このBlockと同じ名前のデータが存在するかチェック
            if (
                dialogueByBlock.TryGetValue(
                    block.BlockName,
                    out List<DialogueLineData> csvLinesForBlock
                )
            )
            {
                // Block内に存在する「Sayコマンドだけ」を順番通りにリストアップ
                List<Say> sayCommandsInBlock = block.CommandList.OfType<Say>().ToList();

                // CSVの行数とSayコマンドの数が一致しない場合、警告を出す
                if (csvLinesForBlock.Count != sayCommandsInBlock.Count)
                {
                    Debug.LogWarning(
                        $"Mismatch Warning: Block '{block.BlockName}' のSayコマンド数 ({sayCommandsInBlock.Count}個) とCSVの行数 ({csvLinesForBlock.Count}行) が一致しません。"
                    );
                }

                // 少ない方の数だけループを回し、エラーを防ぐ
                int loopCount = Mathf.Min(csvLinesForBlock.Count, sayCommandsInBlock.Count);
                for (int i = 0; i < loopCount; i++)
                {
                    Say sayCommand = sayCommandsInBlock[i];
                    DialogueLineData csvLine = csvLinesForBlock[i];

                    // --- 差分更新ロジック ---
                    Character newCharacter = FindCharacter(csvLine.character);

                    // キャラクターがHEROIN_KEYWORDと一致するかどうかで処理を分岐
                    if (csvLine.character == HEROIN_KEYWORD)
                    {
                        // 【Heroineの場合】PortraitString（文字列）を比較・設定する
                        string newPortraitString = csvLine.expression;

                        // 差分チェック：テキスト、キャラクター、または表情文字列が異なれば更新
                        if (sayCommand.GetStandardText() != csvLine.dialogue ||
                            sayCommand._Character != newCharacter ||
                            sayCommand.PortraitString != newPortraitString)
                        {
                            sayCommand.SetStandardText(csvLine.dialogue);
                            sayCommand.SetCharacter(newCharacter);
                            sayCommand.SetPortraitString(newPortraitString); // 新しい文字列設定メソッドを呼び出す
                            sayCommand.SetPortrait(null); // 競合を避けるため、Sprite参照はクリアする
                            totalUpdatedCount++;
                            hasChanged = true;
                        }
                    }
                    else
                    {
                        // 【Heroine以外の場合】従来通りPortrait（Sprite）を比較・設定する
                        Sprite newPortrait = FindPortrait(newCharacter, csvLine.expression);

                        // 差分チェック：テキスト、キャラクター、または立ち絵Spriteが異なれば更新
                        if (sayCommand.GetStandardText() != csvLine.dialogue ||
                            sayCommand._Character != newCharacter ||
                            sayCommand.Portrait != newPortrait)
                        {
                            sayCommand.SetStandardText(csvLine.dialogue);
                            sayCommand.SetCharacter(newCharacter);
                            sayCommand.SetPortrait(newPortrait); // 従来のSprite設定メソッドを呼び出す
                            sayCommand.SetPortraitString(""); // 念のため、文字列はクリアする
                            totalUpdatedCount++;
                            hasChanged = true;
                        }
                    }
                }
            }
        }

        if (hasChanged)
        {
            EditorUtility.SetDirty(targetFlowchart);
        }

        Debug.Log($"チェック完了: {totalUpdatedCount}個のSayコマンドを更新しました。");
    }

    private Character FindCharacter(string name)
    {
        if (string.IsNullOrEmpty(name))
            return null;
        Character[] allCharacters = FindObjectsOfType<Character>();
        foreach (Character character in allCharacters)
        {
            // 名前が一致するCharacterを返す
            // GameObjectの名前で比較するように
            if (character.gameObject.name == name)
                return character;
        }
        Debug.LogWarning($"Character '{name}' がシーンに見つかりません。");
        return null;
    }

    private Sprite FindPortrait(Character character, string expressionName)
    {
        if (character == null || string.IsNullOrEmpty(expressionName))
            return null;
        return character.GetPortrait(expressionName);
    }

    /// <summary>
    /// CSVのフィールド（セル）から前後の空白と、それを囲むダブルクォーテーションを取り除きます。
    /// また、フィールド内部でエスケープされているダブルクォーテーション（""）を元に戻します。
    /// </summary>
    /// <param name="field">処理したい文字列</param>
    /// <returns>整形後の文字列</returns>
    private string SanitizeCsvField(string field)
    {
        if (string.IsNullOrEmpty(field))
        {
            return "";
        }

        string sanitized = field.Trim();

        // 1. フィールドがダブルクォーテーションで囲まれているかチェック
        if (sanitized.StartsWith("\"") && sanitized.EndsWith("\""))
        {
            // 2. 文字数が2文字未満（例: "" や " のみ）の場合は、中身は空として扱う
            if (sanitized.Length < 2)
            {
                return "";
            }

            // 3. 最初と最後のダブルクォーテーションを削除
            sanitized = sanitized.Substring(1, sanitized.Length - 2);

            // 4. 内部でエスケープされている "" を " に置換
            sanitized = sanitized.Replace("\"\"", "\"");
        }


        return sanitized;
    }
}
