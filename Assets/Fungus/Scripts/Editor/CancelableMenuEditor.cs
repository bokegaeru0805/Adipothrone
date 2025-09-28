// This code is part of the Fungus library (https://github.com/snozbot/fungus)
// It is released for free under the MIT open source license (https://github.com/snozbot/fungus/blob/master/LICENSE)

using UnityEditor;

namespace Fungus.EditorUtils
{
    [CustomEditor(typeof(CancelableMenu))]
    public class CancelableMenuEditor : MenuEditor 
    {
        // CancelableMenuはMenuのプロパティ（Text, Target Blockなど）をすべて継承しています。
        // 加えて、新しく追加したプロパティ（cancelKey, cancelIconName）は
        // インスペクター上で編集する必要がない、というご要望でした。
        //
        // そのため、既存のMenuEditorをそのまま継承するだけで、
        // 必要なUIがすべて自動的に描画されます。
        // このクラスの中身は空のままで問題ありません。
        
    }
}