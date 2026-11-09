using System.Collections.Generic;
using Client.Game.InGame.UI.UIState.State;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.Items;

namespace Client.Ui.State.SlotCommands.Block
{
    /// <summary>
    /// 本番の生の値の読み口。SubInventoryState が持つ SubInventoryModel はコントローラと同じ参照なので先書きが即時に見える
    /// Production raw reader; SubInventoryState shares its SubInventoryModel with the controller, so pre-writes are visible at once
    /// </summary>
    public sealed class SubInventoryRawSlots : IRawSubInventorySlots
    {
        private readonly SubInventoryState _subInventoryState;

        public SubInventoryRawSlots(SubInventoryState subInventoryState)
        {
            _subInventoryState = subInventoryState;
        }

        public IReadOnlyList<SlotItem> Read()
        {
            return BlockInventoryPanelStateFactory.ToSlotItems(_subInventoryState.CurrentSubInventory.SubInventory);
        }
    }
}
