using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Fungus;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

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

    /// <summary>
    /// CSVファイルを読み込み、Flowchartの各Block内のSayコマンドを更新します。
    /// </summary>
    public void UpdateDialogue()
    {
        if (targetFlowchart == null || csvFiles.Count == 0)
        {
            Debug.LogError("FlowchartまたはCSVファイルが指定されていません。");
            return;
        }

        var dialogueByBlock = new Dictionary<string, List<DialogueLineData>>();

        foreach (var csvFile in csvFiles)
        {
            if (csvFile == null)
                continue;

            StringReader reader = new StringReader(csvFile.text);
            reader.ReadLine(); // ヘッダー読み飛ばし

            int lineNumber = 1;
            while (reader.Peek() != -1)
            {
                lineNumber++;
                string line = reader.ReadLine();

                // 行内のダブルクォーテーションの数が奇数の場合、改行を含んだセルの途中であると判断し、
                // 偶数になる（＝セルが閉じる）まで次の行を読み込んで結合する
                while (CountChar(line, '"') % 2 != 0 && reader.Peek() != -1)
                {
                    line += "\n" + reader.ReadLine();
                    lineNumber++; // 行数カウントも進める
                }
                // --------------------------------

                string[] values;

                // 改行を含むセルは必ず " で囲まれているため、" が含まれる場合は正規表現スプリットを使用する
                if (line.Contains("\""))
                {
                    // 正規表現：カンマで分割するが、ダブルクォーテーション内のカンマは無視する
                    values = Regex.Split(line, ",(?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)");
                }
                else
                {
                    values = line.Split(',');
                }

                int maxIndex = new[]
                {
                    COL_BLOCK_NAME,
                    COL_CHARACTER,
                    COL_EXPRESSION,
                    COL_DIALOGUE,
                }.Max();

                if (values.Length <= maxIndex)
                {
                    // values.Lengthが足りない場合でも、改行結合のロジックにより
                    // 単純な改行によるズレは解消されているはずです。
                    // それでも足りない場合は本当にCSVの列が足りていません。
                    Debug.LogWarning(
                        $"CSV Warning: {csvFile.name} の {lineNumber}行目付近の列数が不足しています。スキップします。"
                    );
                    continue;
                }

                string blockName = SanitizeCsvField(values[COL_BLOCK_NAME]);
                string characterName = SanitizeCsvField(values[COL_CHARACTER]);
                string expressionName = SanitizeCsvField(values[COL_EXPRESSION]);
                string dialogueText = SanitizeCsvField(values[COL_DIALOGUE]); // ここで改行文字の置換も行う

                if (string.IsNullOrEmpty(blockName) || string.IsNullOrEmpty(characterName))
                    continue;

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

        // --- Step 2以降のFlowchart更新処理は元のまま ---
        UpdateFlowchartBlocks(dialogueByBlock);
    }

    /// <summary>
    /// Flowchartの各Blockを更新する処理をまとめたメソッドです。
    /// </summary>
    /// <param name="dialogueByBlock">Block名をキー、セリフリストを値とする辞書</param>
    /// <returns>更新されたSayコマンドの総数</returns>
    private void UpdateFlowchartBlocks(Dictionary<string, List<DialogueLineData>> dialogueByBlock)
    {
        int totalUpdatedCount = 0;
        bool hasChanged = false;

        foreach (Block block in targetFlowchart.GetComponents<Block>())
        {
            if (
                dialogueByBlock.TryGetValue(
                    block.BlockName,
                    out List<DialogueLineData> csvLinesForBlock
                )
            )
            {
                List<Say> sayCommandsInBlock = block.CommandList.OfType<Say>().ToList();

                if (csvLinesForBlock.Count != sayCommandsInBlock.Count)
                {
                    Debug.LogWarning(
                        $"Mismatch Warning: Block '{block.BlockName}' のSayコマンド数 ({sayCommandsInBlock.Count}) とCSV行数 ({csvLinesForBlock.Count}) が不一致。"
                    );
                }

                int loopCount = Mathf.Min(csvLinesForBlock.Count, sayCommandsInBlock.Count);
                for (int i = 0; i < loopCount; i++)
                {
                    Say sayCommand = sayCommandsInBlock[i];
                    DialogueLineData csvLine = csvLinesForBlock[i];
                    Character newCharacter = FindCharacter(csvLine.character);

                    if (csvLine.character == HEROIN_KEYWORD)
                    {
                        string newPortraitString = csvLine.expression;
                        if (
                            sayCommand.GetStandardText() != csvLine.dialogue
                            || sayCommand._Character != newCharacter
                            || sayCommand.PortraitString != newPortraitString
                        )
                        {
                            sayCommand.SetStandardText(csvLine.dialogue);
                            sayCommand.SetCharacter(newCharacter);
                            sayCommand.SetPortraitString(newPortraitString);
                            sayCommand.SetPortrait(null);
                            totalUpdatedCount++;
                            hasChanged = true;
                        }
                    }
                    else
                    {
                        Sprite newPortrait = FindPortrait(newCharacter, csvLine.expression);
                        if (
                            sayCommand.GetStandardText() != csvLine.dialogue
                            || sayCommand._Character != newCharacter
                            || sayCommand.Portrait != newPortrait
                        )
                        {
                            sayCommand.SetStandardText(csvLine.dialogue);
                            sayCommand.SetCharacter(newCharacter);
                            sayCommand.SetPortrait(newPortrait);
                            sayCommand.SetPortraitString("");
                            totalUpdatedCount++;
                            hasChanged = true;
                        }
                    }
                }
            }
        }

        if (hasChanged)
        {
#if UNITY_EDITOR
            EditorUtility.SetDirty(targetFlowchart);
#endif
        }
        Debug.Log($"チェック完了: {totalUpdatedCount}個のSayコマンドを更新しました。");
    }

    /// <summary>
    /// 指定した文字列内に出現する特定の文字の数をカウントします。
    /// </summary>
    /// <param name="str">対象の文字列</param>
    /// <param name="target">カウントしたい文字</param>
    /// <returns>指定した文字の出現回数</returns>
    private int CountChar(string str, char target)
    {
        if (string.IsNullOrEmpty(str))
            return 0;
        return str.Count(c => c == target);
    }

    /// <summary>
    /// シーン内から指定した名前のCharacterコンポーネントを探します。
    /// </summary>
    /// <param name="name">探したいCharacterの名前</param>
    /// <returns>見つかったCharacterコンポーネント、見つからなかった場合はnull</returns>
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
