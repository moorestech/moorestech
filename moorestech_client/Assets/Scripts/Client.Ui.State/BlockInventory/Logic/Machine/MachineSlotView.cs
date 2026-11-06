using System.Collections.Generic;
using Client.Ui.State.BlockInventory.Logic.Common;
using Core.Master;

namespace Client.Ui.State.BlockInventory.Logic.Machine
{
    // 束縛スロットに描くゴースト（必要なアイテムと個数・受け入れ一覧）
    // The ghost drawn in a bound slot (required item, count and acceptance list)
    public sealed class MachineItemGhost
    {
        public ItemId ItemId { get; }
        public int Count { get; }
        public IReadOnlyList<ItemId> AcceptedItemIds { get; }

        public MachineItemGhost(ItemId itemId, int count, IReadOnlyList<ItemId> acceptedItemIds)
        {
            ItemId = itemId;
            Count = count;
            AcceptedItemIds = acceptedItemIds;
        }

        public bool Accepts(ItemId itemId)
        {
            return AcceptedItems.Contains(AcceptedItemIds, itemId);
        }
    }

    public readonly struct MachineFluidGhost
    {
        public FluidId FluidId { get; }
        public double Amount { get; }

        public MachineFluidGhost(FluidId fluidId, double amount)
        {
            FluidId = fluidId;
            Amount = amount;
        }
    }

    // 描くアイテムスロット。束縛外の残置スロットと束縛の無い機械のスロットはゴースト無しの派生（判断記録 D-5）
    // An item slot to draw; leftovers outside the binding and slots of unbound machines are the ghostless subtype (D-5)
    public abstract class BoundItemSlot
    {
        public int Index { get; }

        protected BoundItemSlot(int index)
        {
            Index = index;
        }

        public static BoundItemSlot WithGhost(int index, MachineItemGhost ghost) => new GhostedItemSlot(index, ghost);
        public static BoundItemSlot WithoutGhost(int index) => new PlainItemSlot(index);
    }

    public sealed class GhostedItemSlot : BoundItemSlot
    {
        public MachineItemGhost Ghost { get; }

        public GhostedItemSlot(int index, MachineItemGhost ghost) : base(index)
        {
            Ghost = ghost;
        }
    }

    public sealed class PlainItemSlot : BoundItemSlot
    {
        public PlainItemSlot(int index) : base(index)
        {
        }
    }

    public abstract class BoundFluidSlot
    {
        public int Index { get; }

        protected BoundFluidSlot(int index)
        {
            Index = index;
        }

        public static BoundFluidSlot WithGhost(int index, MachineFluidGhost ghost) => new GhostedFluidSlot(index, ghost);
        public static BoundFluidSlot WithoutGhost(int index) => new PlainFluidSlot(index);
    }

    public sealed class GhostedFluidSlot : BoundFluidSlot
    {
        public MachineFluidGhost Ghost { get; }

        public GhostedFluidSlot(int index, MachineFluidGhost ghost) : base(index)
        {
            Ghost = ghost;
        }
    }

    public sealed class PlainFluidSlot : BoundFluidSlot
    {
        public PlainFluidSlot(int index) : base(index)
        {
        }
    }

    public sealed class MachineSlotView
    {
        public IReadOnlyList<BoundItemSlot> Inputs { get; }
        public IReadOnlyList<BoundItemSlot> Outputs { get; }
        public IReadOnlyList<BoundFluidSlot> Fluids { get; }

        public MachineSlotView(IReadOnlyList<BoundItemSlot> inputs, IReadOnlyList<BoundItemSlot> outputs, IReadOnlyList<BoundFluidSlot> fluids)
        {
            Inputs = inputs;
            Outputs = outputs;
            Fluids = fluids;
        }
    }

    // 実物入りの束縛入力に出す所持/必要
    // Held/required shown on a filled bound input
    public readonly struct MachineInputRequirement
    {
        public int OwnedCount { get; }
        public int RequiredCount { get; }
        public bool Lacking { get; }

        public MachineInputRequirement(int ownedCount, int requiredCount, bool lacking)
        {
            OwnedCount = ownedCount;
            RequiredCount = requiredCount;
            Lacking = lacking;
        }
    }
}
