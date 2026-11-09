using System;
using System.Collections.Generic;
using Client.Ui.Components.Slots;
using Client.Ui.Content;
using Client.Ui.Foundation.Theme;
using Client.Ui.State.Commands;
using Client.Ui.State.Items;
using Client.Ui.State.SlotCommands.Block;
using Client.Ui.State.Toast;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Parts
{
    /// <summary>
    /// ブロックの枠を持ち物と同じ列数の上限で並べ、押下をブロック側のコマンドへ渡す（Web BlockItemGrid の写し）
    /// Lays out block slots capped at the inventory column count and routes presses to the block commands (port of Web BlockItemGrid)
    /// </summary>
    public sealed class BlockItemGrid : VisualElement
    {
        private const string ClassName = "block-item-grid";
        private const string InventoryColumnsProperty = "--block-item-grid-inventory-columns";

        private readonly string _testId;
        private readonly IBlockSlotCommands _commands;
        private readonly GameContentPresentation _content;
        private readonly UiCommandResultToaster _toaster;
        private readonly SlotGrid _grid;
        private readonly List<(int Index, ItemSlot Slot)> _slots = new();
        private int _inventoryColumns;
        private int _appliedColumns;

        public BlockItemGrid(string testId, IBlockSlotCommands commands, GameContentPresentation content, UiCommandResultToaster toaster)
        {
            _testId = testId;
            _commands = commands;
            _content = content;
            _toaster = toaster;
            _grid = new SlotGrid(testId, 1);
            Add(_grid);
            AddToClassList(ClassName);

            // 列数の上限は持ち物と同じトークン（Web INVENTORY_GRID_COLUMNS）。:root の値は子へ継承されないので自クラスで受け直した名前を読む
            // The column cap is the inventory's own token (Web INVENTORY_GRID_COLUMNS); :root values do not inherit, so read the name re-declared on this class
            RegisterCallback<CustomStyleResolvedEvent>(_ => ReadColumns());

            #region Internal

            void ReadColumns()
            {
                if (UssCustomStyleReader.TryReadNumber(customStyle, InventoryColumnsProperty, out var columns)) _inventoryColumns = (int)columns;
                ApplyColumns();
            }

            #endregion
        }

        public void Render(IReadOnlyList<SlotItem> itemSlots, IReadOnlyList<int> slotIndices)
        {
            // 並ぶ枠の番号が変わったときだけ作り直し、同じなら中身だけ差し替える
            // Rebuild only when the slot indices change; otherwise just swap contents
            if (!SameIndices(slotIndices)) Rebuild(slotIndices);
            foreach (var (index, slot) in _slots) slot.SetContent(ToContent(ReadSlot(index)));
            ApplyColumns();

            #region Internal

            bool SameIndices(IReadOnlyList<int> indices)
            {
                if (indices.Count != _slots.Count) return false;
                for (var i = 0; i < indices.Count; i++)
                {
                    if (indices[i] != _slots[i].Index) return false;
                }
                return true;
            }

            void Rebuild(IReadOnlyList<int> indices)
            {
                _grid.ClearItems();
                _slots.Clear();
                foreach (var index in indices)
                {
                    var slot = new ItemSlot($"{_testId}-slot-{index}");
                    var capturedIndex = index;
                    slot.OnSlotGestured.Subscribe(gesture => OnGestured(capturedIndex, gesture));
                    _grid.AddItem(slot);
                    _slots.Add((index, slot));
                }
                // 並びが変わったので列数を当て直す
                // The layout changed, so apply the column count again
                _appliedColumns = 0;
            }

            // 枠の番号が枠数を超えるのは呼び手（区画）の誤り。購読を例外で止めず空きで描き、LogError で知らせる
            // An index beyond the slot count is the caller's (section's) bug; draw it empty instead of killing the subscription, and LogError
            SlotItem ReadSlot(int index)
            {
                if (0 <= index && index < itemSlots.Count) return itemSlots[index];
                UnityEngine.Debug.LogError($"[BlockItemGrid] {_testId}: slot index {index} is out of range (slot count {itemSlots.Count}); drawn as empty");
                return default;
            }

            ItemSlotContent ToContent(SlotItem item)
            {
                // 空きマスは絵も個数も持たない（ItemSlot が空きの面で描く）
                // Empty cells carry no icon or count (ItemSlot draws the empty face)
                var icon = item.IsEmpty ? null : _content.Item(item.ItemId);
                int? count = item.IsEmpty ? null : item.Count;
                return new ItemSlotContent(icon, count, new ItemSlotMarks(false, false, false), null, null, null);
            }

            void OnGestured(int index, SlotGesture gesture)
            {
                // ブロックの枠は押下・右押下・ダブルクリックだけ（Web useBlockSlotGestures）
                // Block slots react to press, right press and double click only (Web useBlockSlotGestures)
                UiCommandResult result;
                switch (gesture.Kind)
                {
                    case SlotGestureKind.LeftDown: result = _commands.LeftDown(index, gesture.Shift); break;
                    case SlotGestureKind.RightDown: result = _commands.RightDown(index); break;
                    case SlotGestureKind.DoubleClick: result = _commands.DoubleClick(index); break;
                    // silent-ok: 進入とホバーはブロックの枠では使わない（Web と同じ） / 名前のツールチップは ItemSlot が出す
                    default: return;
                }
                _toaster.Report(BlockSlotCommands.CommandName, result);
            }

            #endregion
        }

        private void ApplyColumns()
        {
            // silent-ok: 最初のスタイル解決の前は列数を決めない / CustomStyleResolvedEvent で当て直し、読めなければ UssCustomStyleReader が LogError を出す
            if (_inventoryColumns <= 0) return;
            var columns = Math.Min(_inventoryColumns, Math.Max(1, _slots.Count));
            if (columns == _appliedColumns) return;
            _appliedColumns = columns;
            _grid.SetColumns(columns);
        }
    }
}
