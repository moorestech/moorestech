using System;
using System.Collections.Generic;
using Client.Ui.Components.Slots;
using Client.Ui.Content;
using Client.Ui.Foundation.Theme;
using Client.Ui.Screens.BlockInventory.Parts.Rows;
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
        private readonly KeyedRowList<int, ItemSlot> _slots = new();
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
            if (!_slots.HasSameKeys(slotIndices)) Rebuild(slotIndices);
            for (var i = 0; i < _slots.Count; i++) _slots.RowAt(i).SetContent(SlotItemContent.From(_content, BlockSlotIndexReader.Read(itemSlots, _slots.KeyAt(i), nameof(BlockItemGrid), _testId), false));
            ApplyColumns();

            #region Internal

            void Rebuild(IReadOnlyList<int> indices)
            {
                _grid.ClearItems();
                _slots.Clear();
                foreach (var index in indices)
                {
                    var slot = new ItemSlot($"{_testId}-slot-{index}");
                    var capturedIndex = index;
                    slot.OnSlotGestured.Subscribe(gesture => BlockSlotGestureCommands.Run(_commands, _toaster, capturedIndex, gesture));
                    _grid.AddItem(slot);
                    _slots.Add(index, slot);
                }
                // 並びが変わったので列数を当て直す
                // The layout changed, so apply the column count again
                _appliedColumns = 0;
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
