using System;
using System.Collections.Generic;
using Client.Ui.Components.Slots;
using Client.Ui.Content;
using Client.Ui.Screens.BlockInventory.Parts;
using Client.Ui.Screens.BlockInventory.Parts.Rows;
using Client.Ui.State.BlockInventory.Logic.Machine;
using Client.Ui.State.Items;
using Client.Ui.State.SlotCommands.Block;
using Client.Ui.State.Toast;
using UniRx;

namespace Client.Ui.Screens.BlockInventory.Machine.Slots
{
    /// <summary>
    /// 機械の枠の帯 1 本（入力・出力・モジュール）。並ぶ番地が同じ間は枠の要素と購読を残して中身だけ当て直す（D-37）
    /// One machine slot band (input, output or module); keeps the slot elements and subscriptions while the indices are unchanged and only reapplies contents (D-37)
    /// </summary>
    public sealed class MachineSlotBand
    {
        private readonly string _slotTestidPrefix;
        private readonly IBlockSlotCommands _commands;
        private readonly UiCommandResultToaster _toaster;
        private readonly GameContentPresentation _content;
        private readonly KeyedRowList<int, ItemSlot> _slots = new();
        private readonly CompositeDisposable _subscriptions = new();

        public SlotGrid Grid { get; }

        public MachineSlotBand(string gridTestid, string slotTestidPrefix, IBlockSlotCommands commands, UiCommandResultToaster toaster, GameContentPresentation content)
        {
            _slotTestidPrefix = slotTestidPrefix;
            _commands = commands;
            _toaster = toaster;
            _content = content;
            Grid = new SlotGrid(gridTestid, 1);
        }

        public void Render(IReadOnlyList<BoundItemSlot> bound, IReadOnlyList<SlotItem> itemSlots, bool showsRequirement)
        {
            // 加工中は状態更新が毎 tick 届くので、番地が変わったときだけ作り直す（ダブルクリックの数え直しを防ぐ）
            // Updates arrive every tick while processing, so rebuild only when the indices change (avoids resetting the double-click count)
            if (!_slots.HasSameKeys(BoundIndices())) Rebuild();
            for (var i = 0; i < _slots.Count; i++) _slots.RowAt(i).SetContent(MachineSlotContents.For(BlockSlotIndexReader.Read(itemSlots, _slots.KeyAt(i), nameof(MachineSlotBand), _slotTestidPrefix), bound[i], showsRequirement, _content));

            #region Internal

            int[] BoundIndices()
            {
                var indices = new int[bound.Count];
                for (var i = 0; i < indices.Length; i++) indices[i] = bound[i].Index;
                return indices;
            }

            void Rebuild()
            {
                Grid.ClearItems();
                _slots.Clear();
                _subscriptions.Clear();
                for (var position = 0; position < bound.Count; position++)
                {
                    // 枠の testid は Web に無い名前（Web は帯の子の順で引く）。録画とテストの照準のために付ける
                    // The slot testid is not on the Web (it picks children of the band by order); added as an aim for recordings and tests
                    var slot = new ItemSlot($"{_slotTestidPrefix}-{position}");
                    var blockSlot = bound[position].Index;
                    slot.OnSlotGestured.Subscribe(gesture => BlockSlotGestureCommands.Run(_commands, _toaster, blockSlot, gesture)).AddTo(_subscriptions);
                    Grid.AddItem(slot);
                    _slots.Add(blockSlot, slot);
                }
                Grid.SetColumns(Math.Max(1, bound.Count));
            }

            #endregion
        }
    }
}
