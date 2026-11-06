using System.Collections.Generic;
using Client.Ui.State.BlockInventory.Logic.Machine;
using Core.Master;

namespace Client.Ui.State.BlockInventory.Logic.SlotRestriction
{
    // 入れ口固定ブロック（発射台・宇宙エレベーター）の入れ口 1 つの受け入れ
    // What one inlet of a fixed-inlet block (launch pad, space elevator) accepts
    public readonly struct InletSlotBinding
    {
        public int Slot { get; }
        public IReadOnlyList<ItemId> AcceptedItemIds { get; }

        public InletSlotBinding(int slot, IReadOnlyList<ItemId> acceptedItemIds)
        {
            Slot = slot;
            AcceptedItemIds = acceptedItemIds;
        }
    }

    /// <summary>
    /// Shift 配分の判断が読むブロック在庫の部分（Web の BlockInventoryData のうち blockSlotRestriction が読む所）。開いているか・詳細が届いたかを sealed 派生で表す（判断記録 D-5）
    /// The part of the block inventory the Shift-allocation decision reads (what Web blockSlotRestriction reads of BlockInventoryData); open-or-not and detail arrival are sealed subtypes (D-5)
    /// </summary>
    public abstract class BlockSlotRestrictionInput
    {
        public static readonly BlockSlotRestrictionInput NotOpenBlock = new NotOpenBlockSlotInput();

        private protected BlockSlotRestrictionInput()
        {
        }

        // 開いたブロック在庫。詳細（入れ口の束縛か機械の束縛）は届いた時点で With* で足す
        // An open block inventory; the detail (inlet or machine bindings) is added with With* once it arrives
        public static OpenBlockSlotInput OpenBlock(string blockType, bool openFailed, int itemSlotCount)
        {
            return new OpenBlockSlotInput(blockType, openFailed, itemSlotCount, BlockSlotDetail.Pending);
        }
    }

    public sealed class NotOpenBlockSlotInput : BlockSlotRestrictionInput
    {
        internal NotOpenBlockSlotInput()
        {
        }
    }

    public sealed class OpenBlockSlotInput : BlockSlotRestrictionInput
    {
        public string BlockType { get; }
        public bool OpenFailed { get; }
        public int ItemSlotCount { get; }
        public BlockSlotDetail Detail { get; }

        internal OpenBlockSlotInput(string blockType, bool openFailed, int itemSlotCount, BlockSlotDetail detail)
        {
            BlockType = blockType;
            OpenFailed = openFailed;
            ItemSlotCount = itemSlotCount;
            Detail = detail;
        }

        public OpenBlockSlotInput WithInletBindings(IReadOnlyList<InletSlotBinding> inletBindings)
        {
            return new OpenBlockSlotInput(BlockType, OpenFailed, ItemSlotCount, new InletBlockSlotDetail(inletBindings));
        }

        public OpenBlockSlotInput WithMachine(MachineBindingDetail machine)
        {
            return new OpenBlockSlotInput(BlockType, OpenFailed, ItemSlotCount, new MachineBlockSlotDetail(machine));
        }
    }

    // 開いたブロックの詳細。未着・入れ口の束縛・機械の束縛のどれか 1 つ
    // An open block's detail: exactly one of pending, inlet bindings or machine bindings
    public abstract class BlockSlotDetail
    {
        public static readonly BlockSlotDetail Pending = new PendingBlockSlotDetail();

        private protected BlockSlotDetail()
        {
        }
    }

    public sealed class PendingBlockSlotDetail : BlockSlotDetail
    {
        internal PendingBlockSlotDetail()
        {
        }
    }

    public sealed class InletBlockSlotDetail : BlockSlotDetail
    {
        public IReadOnlyList<InletSlotBinding> InletBindings { get; }

        internal InletBlockSlotDetail(IReadOnlyList<InletSlotBinding> inletBindings)
        {
            InletBindings = inletBindings;
        }
    }

    public sealed class MachineBlockSlotDetail : BlockSlotDetail
    {
        public MachineBindingDetail Machine { get; }

        internal MachineBlockSlotDetail(MachineBindingDetail machine)
        {
            Machine = machine;
        }
    }
}
