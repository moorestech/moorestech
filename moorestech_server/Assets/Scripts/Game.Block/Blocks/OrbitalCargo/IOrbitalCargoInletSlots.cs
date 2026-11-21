using System.Collections.Generic;
using Core.Inventory;
using Core.Master;
using Game.Block.Interface.Component;

namespace Game.Block.Blocks.OrbitalCargo
{
    // 打ち上げ口ごとの入れ口の事情。選択部品は積荷の語彙だけを持ち、スロットの並びはここが持つ
    // Each launch site's inlet details; the selector only speaks cargo, the slot layout lives here
    internal interface IOrbitalCargoInletSlots
    {
        IReadOnlyCollection<ItemId> CarriedPayloadItemIds { get; }

        // 選んだ種類以外の入れ口の中身を返却先へ移し、入らない分はスロットに残す
        // Move inlet contents of kinds other than the selected one to the refund target; leftovers stay in the slot
        void RefundUnselectedPayloads(SelectedOrbitalCargo selection, IOpenableInventory refundInventory);
    }
}
