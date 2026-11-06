using System.Collections.Generic;
using Client.Ui.State.BlockInventory.Logic.Detail;
using Core.Master;

namespace Client.Ui.State.BlockInventory.Logic.Machine
{
    // ホストが決めたアイテムスロットの束縛。AcceptedItemIds は上位変種を含む受け入れ一覧
    // A host-decided item slot binding; AcceptedItemIds lists accepted items including higher-level variants
    public sealed class MachineItemSlotBinding
    {
        public int Slot { get; }
        public ItemId ItemId { get; }
        public int Count { get; }
        public IReadOnlyList<ItemId> AcceptedItemIds { get; }

        public MachineItemSlotBinding(int slot, ItemId itemId, int count, IReadOnlyList<ItemId> acceptedItemIds)
        {
            Slot = slot;
            ItemId = itemId;
            Count = count;
            AcceptedItemIds = acceptedItemIds;
        }
    }

    // 液体タンクの束縛。出力タンクは入力タンク数の後ろの番号を指す
    // A fluid tank binding; output tanks are numbered after the input tanks
    public readonly struct MachineTankBinding
    {
        public int Tank { get; }
        public FluidId FluidId { get; }
        public double Amount { get; }

        public MachineTankBinding(int tank, FluidId fluidId, double amount)
        {
            Tank = tank;
            FluidId = fluidId;
            Amount = amount;
        }
    }

    /// <summary>
    /// 機械のスロット構成と束縛（Web の MachineDetailData のうちスロットの描画が読む部分）
    /// A machine's slot layout and bindings (the part of Web MachineDetailData that slot drawing reads)
    /// </summary>
    public sealed class MachineBindingDetail
    {
        public MachineSlotLayout Layout { get; }
        public IReadOnlyList<MachineItemSlotBinding> SlotBindings { get; }
        public IReadOnlyList<MachineTankBinding> TankBindings { get; }

        public MachineBindingDetail(MachineSlotLayout layout, IReadOnlyList<MachineItemSlotBinding> slotBindings, IReadOnlyList<MachineTankBinding> tankBindings)
        {
            Layout = layout;
            SlotBindings = slotBindings;
            TankBindings = tankBindings;
        }
    }
}
