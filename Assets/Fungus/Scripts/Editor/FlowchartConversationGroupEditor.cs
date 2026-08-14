using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Fungus.EditorUtils
{
    internal static class FlowchartConversationGroupEditor
    {
        // 左列の最も長いBlockの右端から、次の列までに空ける隙間です。
        private const float ColumnSpacing = 75f;

        // 同じ列に並ぶBlock同士の縦方向の間隔です。
        private const float RowSpacing = 50f;

        // Block群とグループ枠の間に確保する余白です。
        private const float FramePadding = 20f;

        // グループタイトルを表示する見出し部分の高さです。
        private const float HeaderHeight = 40f;

        // グループタイトルの文字サイズです。
        private const int TitleFontSize = 18;

        // グループ枠線の不透明度です。
        private const float FrameBorderAlpha = 0.9f;

        // タイトル背景の不透明度です。
        private const float HeaderBackgroundAlpha = 0.85f;

        // 挿入位置を示すガイド線の太さです。
        private const float InsertionGuideHeight = 6f;

        // ガイド線の中心を挿入位置へ合わせるための縦方向オフセットです。
        private const float InsertionGuideVerticalOffset = InsertionGuideHeight / 2f;

        // 挿入位置を示すガイド線の横幅です。
        private const float InsertionGuideWidth = FlowchartWindow.BlockMaxWidth;

        // グループ外にBlockを作成するとき、枠の右端から離す距離です。
        private const float OutsideBlockSpacing = 60f;

        // ドロップ位置から列・行を切り替える境界です。0.5は間隔の中央を表します。
        private const float DropSelectionThreshold = 0.5f;

        // 挿入位置を示すガイド線の色です。
        private static readonly Color InsertionGuideColor = new Color(0.25f, 1f, 0.45f, 1f);

        // 所属Blockがすべて選択され、グループ単位で移動できるときの枠色です。
        private static readonly Color SelectedGroupBorderColor = new Color(1f, 0.65f, 0.15f, 1f);

        private static readonly List<FlowchartConversationGroups.Group> draggedGroups
            = new List<FlowchartConversationGroups.Group>();
        private static Vector2 previousMousePosition;
        private static FlowchartConversationGroups.Group editingGroup;
        private static Flowchart previewFlowchart;
        private static FlowchartConversationGroups.Group previewGroup;
        private static Block previewBlock;
        private static int previewColumn;
        private static int previewIndex;
        private static GUIStyle titleStyle;

        private static GUIStyle TitleStyle
        {
            get
            {
                if (titleStyle == null)
                {
                    titleStyle = new GUIStyle(EditorStyles.boldLabel)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        fontSize = TitleFontSize,
                        fontStyle = FontStyle.Bold
                    };
                }
                return titleStyle;
            }
        }

        internal static void Draw(Flowchart flowchart)
        {
            var data = flowchart.GetComponent<FlowchartConversationGroups>();
            if (data == null) return;

            foreach (var group in data.Groups)
            {
                Rect frameRect = GetFrameRect(group);
                frameRect.position += flowchart.ScrollPos;
                EditorGUI.DrawRect(frameRect, group.color);

                bool isFullySelected = IsFullySelected(group, flowchart.SelectedBlocks);
                Color border = isFullySelected ? SelectedGroupBorderColor : group.color;
                if (!isFullySelected) border.a = FrameBorderAlpha;
                Handles.DrawSolidRectangleWithOutline(frameRect, Color.clear, border);

                Rect headerRect = GetHeaderRect(group);
                headerRect.position += flowchart.ScrollPos;
                EditorGUI.DrawRect(
                    headerRect,
                    new Color(border.r, border.g, border.b, HeaderBackgroundAlpha)
                );

                if (editingGroup == group)
                {
                    GUI.SetNextControlName("ConversationGroupTitle");
                    string newTitle = GUI.TextField(headerRect, group.title, TitleStyle);
                    if (newTitle != group.title)
                    {
                        Undo.RecordObject(data, "Rename Conversation Group");
                        group.title = newTitle;
                        EditorUtility.SetDirty(data);
                    }
                    EditorGUI.FocusTextInControl("ConversationGroupTitle");
                }
                else
                {
                    GUI.Label(headerRect, group.title, TitleStyle);
                }
            }
        }

        internal static void DrawInsertionPreview(Flowchart flowchart)
        {
            if (previewFlowchart != flowchart || previewGroup == null) return;

            Rect insertionRect = new Rect(
                GetColumnStartX(previewGroup, previewColumn, previewBlock),
                previewGroup.contentPosition.y + previewIndex * RowSpacing
                    - InsertionGuideVerticalOffset,
                InsertionGuideWidth,
                InsertionGuideHeight
            );
            insertionRect.position += flowchart.ScrollPos;
            EditorGUI.DrawRect(insertionRect, InsertionGuideColor);
        }

        internal static bool HandleEvent(FlowchartWindow window, Flowchart flowchart, Event e)
        {
            var data = flowchart.GetComponent<FlowchartConversationGroups>();
            if (data == null) return false;

            Vector2 graphMouse = e.mousePosition / flowchart.Zoom - flowchart.ScrollPos;
            if (editingGroup != null && e.type == EventType.KeyDown
                && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter
                    || e.keyCode == KeyCode.Escape))
            {
                editingGroup = null;
                GUI.FocusControl(null);
                e.Use();
                window.Repaint();
                return true;
            }

            if (e.type == EventType.MouseDown && e.button == 0)
            {
                ClearInsertionPreview();
                draggedGroups.Clear();
                var group = GetGroupAtHeader(data, graphMouse);
                if (group == null)
                {
                    editingGroup = null;
                    return false;
                }

                if (e.clickCount == 2)
                {
                    editingGroup = group;
                    e.Use();
                    window.Repaint();
                    return true;
                }

                if (IsFullySelected(group, flowchart.SelectedBlocks))
                {
                    draggedGroups.AddRange(
                        GetFullySelectedGroups(data, flowchart.SelectedBlocks)
                    );
                }
                else
                {
                    draggedGroups.Add(group);
                }

                previousMousePosition = graphMouse;
                Undo.RecordObject(data, "Move Conversation Groups");
                foreach (var member in draggedGroups.SelectMany(item => item.members))
                {
                    if (member.block != null)
                        Undo.RecordObject(member.block, "Move Conversation Groups");
                }
                e.Use();
                return true;
            }

            if (e.type == EventType.MouseDrag && e.button == 0 && draggedGroups.Count > 0)
            {
                Vector2 delta = graphMouse - previousMousePosition;
                foreach (var group in draggedGroups)
                {
                    group.contentPosition += delta;
                    foreach (var member in group.members)
                    {
                        if (member.block == null) continue;
                        Rect rect = member.block._NodeRect;
                        rect.position += delta;
                        member.block._NodeRect = rect;
                        EditorUtility.SetDirty(member.block);
                    }
                }
                previousMousePosition = graphMouse;
                EditorUtility.SetDirty(data);
                e.Use();
                window.Repaint();
                return true;
            }

            if (e.rawType == EventType.MouseUp && e.button == 0 && draggedGroups.Count > 0)
            {
                draggedGroups.Clear();
                e.Use();
                return true;
            }
            return false;
        }

        internal static bool TryShowGroupMenu(FlowchartWindow window, Flowchart flowchart, Vector2 mouse)
        {
            var data = flowchart.GetComponent<FlowchartConversationGroups>();
            if (data == null) return false;
            var group = GetGroupAtHeader(data, mouse / flowchart.Zoom - flowchart.ScrollPos);
            if (group == null) return false;

            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Edit Title"), false, () =>
            {
                editingGroup = group;
                window.Repaint();
            });
            menu.AddItem(new GUIContent("Arrange Blocks"), false, () =>
            {
                Undo.RecordObject(data, "Arrange Conversation Group");
                Arrange(data, group);
                window.Repaint();
            });
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Add Block/At Start"), false,
                () => AddBlockToGroup(window, flowchart, data, group, true));
            menu.AddItem(new GUIContent("Add Block/At End"), false,
                () => AddBlockToGroup(window, flowchart, data, group, false));
            menu.AddItem(new GUIContent("Add Block/Outside Group"), false, () =>
            {
                Rect frame = GetFrameRect(group);
                window.CreateBlock(
                    flowchart,
                    new Vector2(frame.xMax + OutsideBlockSpacing, frame.yMin)
                );
                window.Repaint();
            });
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Delete Group (Keep Blocks)"), false, () =>
            {
                Undo.RecordObject(data, "Delete Conversation Group");
                data.Groups.Remove(group);
                EditorUtility.SetDirty(data);
                window.Repaint();
            });
            menu.ShowAsContext();
            return true;
        }

        internal static void AddBlockMenuItems(GenericMenu menu, FlowchartWindow window,
            Flowchart flowchart, Block clickedBlock)
        {
            menu.AddSeparator("");
            var selected = flowchart.SelectedBlocks.Where(block => block != null).Distinct().ToList();
            if (!selected.Contains(clickedBlock)) selected.Add(clickedBlock);
            menu.AddItem(new GUIContent("Conversation Group/Create from Selection"), false,
                () => CreateGroup(window, flowchart, selected));

            var data = flowchart.GetComponent<FlowchartConversationGroups>();
            var group = data == null ? null : data.Groups.FirstOrDefault(candidate =>
                candidate.members.Any(member => member.block == clickedBlock));
            if (group == null)
            {
                menu.AddDisabledItem(new GUIContent("Conversation Group/Remove from Group"));
                return;
            }

            menu.AddItem(new GUIContent("Conversation Group/Remove from Group"), false, () =>
            {
                Undo.RecordObject(data, "Remove Block From Conversation Group");
                group.members.RemoveAll(member => member.block == clickedBlock);
                if (group.members.Count == 0) data.Groups.Remove(group);
                else Arrange(data, group);
                EditorUtility.SetDirty(data);
                window.Repaint();
            });
        }

        internal static void OnBlocksDragging(Flowchart flowchart, IList<Block> movedBlocks)
        {
            ClearInsertionPreview();
            var data = flowchart.GetComponent<FlowchartConversationGroups>();
            if (data == null || movedBlocks == null) return;

            var fullySelectedBlocks = new HashSet<Block>(
                GetFullySelectedGroups(data, movedBlocks)
                    .SelectMany(group => group.members)
                    .Where(member => member.block != null)
                    .Select(member => member.block)
            );
            var block = movedBlocks.FirstOrDefault(item =>
                item != null && !fullySelectedBlocks.Contains(item));
            if (block == null) return;

            var sourceGroup = FindContainingGroup(data, block);
            var targetGroup = FindDropGroup(data, block, sourceGroup) ?? sourceGroup;
            if (targetGroup == null) return;

            previewFlowchart = flowchart;
            previewGroup = targetGroup;
            previewBlock = block;
            previewColumn = GetDropColumn(targetGroup, block);
            previewIndex = GetDropRow(targetGroup, block);
        }

        internal static void OnBlocksMoved(Flowchart flowchart, IList<Block> movedBlocks)
        {
            ClearInsertionPreview();
            var data = flowchart.GetComponent<FlowchartConversationGroups>();
            if (data == null || movedBlocks == null) return;

            var affectedGroups = new HashSet<FlowchartConversationGroups.Group>();
            var fullySelectedGroups = GetFullySelectedGroups(data, movedBlocks);
            var groupMovedBlocks = new HashSet<Block>();
            var validMovedBlocks = movedBlocks.Where(block => block != null)
                .OrderBy(block => block._NodeRect.y).ToList();
            if (validMovedBlocks.Count == 0) return;

            Undo.RecordObject(data, "Move Or Insert Conversation Blocks");
            foreach (var group in fullySelectedGroups)
            {
                var referenceMember = group.members.First(member => member.block != null);
                Vector2 expectedPosition = new Vector2(
                    GetColumnStartX(group, referenceMember.column),
                    group.contentPosition.y + referenceMember.order * RowSpacing
                );
                Vector2 movementDelta = referenceMember.block._NodeRect.position - expectedPosition;
                group.contentPosition += movementDelta;

                foreach (var member in group.members)
                {
                    if (member.block != null) groupMovedBlocks.Add(member.block);
                }
            }

            validMovedBlocks.RemoveAll(block => groupMovedBlocks.Contains(block));
            foreach (var block in validMovedBlocks)
            {
                var sourceGroup = FindContainingGroup(data, block);
                var targetGroup = FindDropGroup(data, block, sourceGroup) ?? sourceGroup;
                if (targetGroup == null) continue;

                FlowchartConversationGroups.Member member = null;
                if (sourceGroup != null)
                {
                    member = sourceGroup.members.FirstOrDefault(item => item.block == block);
                    sourceGroup.members.Remove(member);
                    affectedGroups.Add(sourceGroup);
                }

                if (member == null)
                    member = new FlowchartConversationGroups.Member { block = block };

                int column = GetDropColumn(targetGroup, block);
                int insertionRow = GetDropRow(targetGroup, block);
                member.column = column;

                var columnMembers = targetGroup.members.Where(item => item.column == column)
                    .OrderBy(item => item.order).ToList();
                if (columnMembers.Any(item => item.order == insertionRow))
                {
                    foreach (var existingMember in columnMembers
                        .Where(item => item.order >= insertionRow))
                    {
                        existingMember.order++;
                    }
                }
                member.order = insertionRow;

                targetGroup.members.Add(member);
                affectedGroups.Add(targetGroup);
            }

            foreach (var group in affectedGroups)
            {
                if (group.members.Count == 0)
                {
                    data.Groups.Remove(group);
                    continue;
                }
                CompactColumns(group);
                Arrange(data, group);
            }
            EditorUtility.SetDirty(data);
        }

        private static void CreateGroup(FlowchartWindow window, Flowchart flowchart, List<Block> blocks)
        {
            if (blocks.Count == 0) return;
            var data = flowchart.GetComponent<FlowchartConversationGroups>();
            if (data == null) data = Undo.AddComponent<FlowchartConversationGroups>(flowchart.gameObject);

            Undo.RecordObject(data, "Create Conversation Group");
            foreach (var existing in data.Groups)
                existing.members.RemoveAll(member => blocks.Contains(member.block));
            data.Groups.RemoveAll(group => group.members.Count == 0);

            var group = new FlowchartConversationGroups.Group
            {
                contentPosition = new Vector2(blocks.Min(block => block._NodeRect.x),
                    blocks.Min(block => block._NodeRect.y))
            };
            int order = 0;
            foreach (var block in blocks.OrderBy(block => block._NodeRect.y))
            {
                group.members.Add(new FlowchartConversationGroups.Member
                {
                    block = block,
                    order = order++
                });
            }
            data.Groups.Add(group);
            Arrange(data, group);
            editingGroup = group;
            EditorUtility.SetDirty(data);
            window.Repaint();
        }

        private static void Arrange(FlowchartConversationGroups data,
            FlowchartConversationGroups.Group group)
        {
            RemoveMissingMembers(group);
            foreach (var column in group.members.GroupBy(member => member.column))
            {
                var ordered = column.OrderBy(member => member.order).ToList();
                foreach (var member in ordered)
                {
                    Undo.RecordObject(member.block, "Arrange Conversation Group");
                    Rect rect = member.block._NodeRect;
                    rect.position = new Vector2(
                        GetColumnStartX(group, member.column),
                        group.contentPosition.y + member.order * RowSpacing
                    );
                    member.block._NodeRect = rect;
                    EditorUtility.SetDirty(member.block);
                }
            }
            EditorUtility.SetDirty(data);
        }

        private static void AddBlockToGroup(FlowchartWindow window, Flowchart flowchart,
            FlowchartConversationGroups data, FlowchartConversationGroups.Group group,
            bool insertAtStart)
        {
            Undo.RecordObject(data, "Add Block To Conversation Group");
            int column = insertAtStart || group.members.Count == 0
                ? 0
                : group.members.Max(member => member.column);
            var columnMembers = group.members.Where(member => member.column == column)
                .OrderBy(member => member.order).ToList();
            int insertionRow = insertAtStart || columnMembers.Count == 0
                ? 0
                : columnMembers.Max(member => member.order) + 1;
            foreach (var member in columnMembers.Where(member => member.order >= insertionRow))
                member.order++;

            var block = window.CreateBlock(flowchart, group.contentPosition);
            group.members.Add(new FlowchartConversationGroups.Member
            {
                block = block,
                column = column,
                order = insertionRow
            });
            Arrange(data, group);
            window.Repaint();
        }

        private static FlowchartConversationGroups.Group FindContainingGroup(
            FlowchartConversationGroups data, Block block)
        {
            return data.Groups.FirstOrDefault(group =>
                group.members.Any(member => member.block == block));
        }

        private static List<FlowchartConversationGroups.Group> GetFullySelectedGroups(
            FlowchartConversationGroups data,
            IList<Block> selectedBlocks)
        {
            if (selectedBlocks == null) return new List<FlowchartConversationGroups.Group>();

            var selectedBlockSet = new HashSet<Block>(selectedBlocks.Where(block => block != null));
            return data.Groups.Where(group => IsFullySelected(group, selectedBlockSet)).ToList();
        }

        private static bool IsFullySelected(
            FlowchartConversationGroups.Group group,
            IEnumerable<Block> selectedBlocks)
        {
            if (group == null || selectedBlocks == null) return false;

            var validMembers = group.members.Where(member => member.block != null).ToList();
            if (validMembers.Count == 0) return false;

            var selectedBlockSet = selectedBlocks as HashSet<Block>
                ?? new HashSet<Block>(selectedBlocks.Where(block => block != null));
            return validMembers.All(member => selectedBlockSet.Contains(member.block));
        }

        private static FlowchartConversationGroups.Group FindDropGroup(
            FlowchartConversationGroups data,
            Block block,
            FlowchartConversationGroups.Group sourceGroup)
        {
            Vector2 center = block._NodeRect.center;
            for (int index = data.Groups.Count - 1; index >= 0; index--)
            {
                var candidate = data.Groups[index];
                if (candidate == sourceGroup) continue;
                if (GetDropRect(candidate).Contains(center)) return candidate;
            }

            if (sourceGroup == null)
            {
                for (int index = data.Groups.Count - 1; index >= 0; index--)
                {
                    if (GetDropRect(data.Groups[index]).Contains(center)) return data.Groups[index];
                }
            }
            return null;
        }

        private static int GetDropColumn(FlowchartConversationGroups.Group group, Block block)
        {
            var remainingMembers = group.members.Where(member =>
                member.block != null && member.block != block).ToList();
            if (remainingMembers.Count == 0) return 0;

            int lastColumn = remainingMembers.Max(member => member.column);
            float blockX = block._NodeRect.x;
            for (int column = 0; column <= lastColumn; column++)
            {
                float columnRight = GetColumnStartX(group, column, block)
                    + GetColumnWidth(group, column, block);
                float boundary = columnRight + ColumnSpacing * DropSelectionThreshold;
                if (blockX < boundary) return column;
            }
            return lastColumn + 1;
        }

        private static float GetColumnStartX(
            FlowchartConversationGroups.Group group,
            int column,
            Block ignoredBlock = null)
        {
            float x = group.contentPosition.x;
            for (int currentColumn = 0; currentColumn < column; currentColumn++)
            {
                x += GetColumnWidth(group, currentColumn, ignoredBlock) + ColumnSpacing;
            }
            return x;
        }

        private static float GetColumnWidth(
            FlowchartConversationGroups.Group group,
            int column,
            Block ignoredBlock = null)
        {
            var widths = group.members.Where(member =>
                    member.block != null
                    && member.block != ignoredBlock
                    && member.column == column)
                .Select(member => member.block._NodeRect.width);
            return Mathf.Max(FlowchartWindow.BlockMinWidth, widths.DefaultIfEmpty(0f).Max());
        }

        private static int GetDropRow(FlowchartConversationGroups.Group group, Block block)
        {
            float relativeY = block._NodeRect.y - group.contentPosition.y;
            int index = Mathf.FloorToInt(
                (relativeY + RowSpacing * DropSelectionThreshold) / RowSpacing
            );
            return Mathf.Max(0, index);
        }

        private static Rect GetDropRect(FlowchartConversationGroups.Group group)
        {
            Rect rect = GetFrameRect(group);
            rect.xMax += ColumnSpacing + FlowchartWindow.BlockMaxWidth;
            rect.yMax += RowSpacing;
            return rect;
        }

        private static void ClearInsertionPreview()
        {
            previewFlowchart = null;
            previewGroup = null;
            previewBlock = null;
            previewColumn = 0;
            previewIndex = 0;
        }

        private static void CompactColumns(FlowchartConversationGroups.Group group)
        {
            var columns = group.members.Select(member => member.column).Distinct().OrderBy(value => value).ToList();
            var map = columns.Select((value, index) => new { value, index })
                .ToDictionary(pair => pair.value, pair => pair.index);
            foreach (var member in group.members) member.column = map[member.column];
        }

        private static FlowchartConversationGroups.Group GetGroupAtHeader(
            FlowchartConversationGroups data, Vector2 mouse)
        {
            for (int index = data.Groups.Count - 1; index >= 0; index--)
                if (GetHeaderRect(data.Groups[index]).Contains(mouse)) return data.Groups[index];
            return null;
        }

        private static Rect GetHeaderRect(FlowchartConversationGroups.Group group)
        {
            Rect frame = GetFrameRect(group);
            return new Rect(frame.x, frame.y - HeaderHeight, frame.width, HeaderHeight);
        }

        private static Rect GetFrameRect(FlowchartConversationGroups.Group group)
        {
            var blocks = group.members.Where(member => member.block != null)
                .Select(member => member.block).ToList();
            if (blocks.Count == 0) return new Rect(group.contentPosition, Vector2.zero);
            return Rect.MinMaxRect(
                Mathf.Min(group.contentPosition.x, blocks.Min(block => block._NodeRect.xMin))
                    - FramePadding,
                Mathf.Min(group.contentPosition.y, blocks.Min(block => block._NodeRect.yMin))
                    - FramePadding,
                blocks.Max(block => block._NodeRect.xMax) + FramePadding,
                blocks.Max(block => block._NodeRect.yMax) + FramePadding);
        }

        private static void RemoveMissingMembers(FlowchartConversationGroups.Group group)
        {
            group.members.RemoveAll(member => member == null || member.block == null);
        }
    }
}
