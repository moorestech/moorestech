using System;
using System.Collections.Generic;
using Client.Ui.State.BlockInventory.Logic.Machine;
using Core.Master;
using Mooresmaster.Model.BlocksModule;

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
    /// Shift 配分の判断が読むブロック在庫の部分（Web の BlockInventoryData のうち blockSlotRestriction が読む所）。開いた・開くのに失敗したを sealed 派生で表す（判断記録 D-5）
    /// The part of the block inventory the Shift-allocation decision reads (what Web blockSlotRestriction reads of BlockInventoryData); open and open-failed are sealed subtypes (D-5)
    /// </summary>
    public abstract class BlockSlotRestrictionInput
    {
        public static readonly BlockSlotRestrictionInput OpenFailedBlock = new OpenFailedBlockSlotInput();

        private protected BlockSlotRestrictionInput()
        {
        }

        // 開いたブロック在庫。詳細（入れ口の束縛か機械の束縛）は届いた時点で With* で足す
        // An open block inventory; the detail (inlet or machine bindings) is added with With* once it arrives
        public static OpenBlockSlotInput OpenBlock(string blockType, int itemSlotCount)
        {
            return new OpenBlockSlotInput(blockType, itemSlotCount, BlockSlotDetail.Pending);
        }
    }

    // 在庫を開くのに失敗した（種別も詳細も持たない。取得失敗は候補探索の前に返すため）
    // The inventory failed to open (no type or detail; the failure is returned before any candidate lookup)
    public sealed class OpenFailedBlockSlotInput : BlockSlotRestrictionInput
    {
        internal OpenFailedBlockSlotInput()
        {
        }
    }

    public sealed class OpenBlockSlotInput : BlockSlotRestrictionInput
    {
        public string BlockType { get; }
        public int ItemSlotCount { get; }
        public BlockSlotDetail Detail { get; }

        // 入れ口固定の種別（発射台・宇宙エレベーター）か
        // Whether the type has fixed inlets (launch pad, space elevator)
        public bool IsFixedInletBlock => IsFixedInletBlockType(BlockType);

        internal OpenBlockSlotInput(string blockType, int itemSlotCount, BlockSlotDetail detail)
        {
            BlockType = blockType;
            ItemSlotCount = itemSlotCount;
            Detail = detail;
        }

        // 種別と食い違う詳細は組み立てた時点で止める（結線ミスが黙って拒否や無制限になるのを防ぐ）
        // Reject a detail that disagrees with the type at build time so a wiring mistake cannot silently refuse or unrestrict
        public OpenBlockSlotInput WithInletBindings(IReadOnlyList<InletSlotBinding> inletBindings)
        {
            if (!IsFixedInletBlock) throw new ArgumentException($"InletBindings need a fixed-inlet block type, but was {BlockType}", nameof(inletBindings));
            return new OpenBlockSlotInput(BlockType, ItemSlotCount, new InletBlockSlotDetail(inletBindings));
        }

        public OpenBlockSlotInput WithMachine(MachineBindingDetail machine)
        {
            if (IsFixedInletBlock) throw new ArgumentException($"Machine bindings do not fit the fixed-inlet block type {BlockType}", nameof(machine));
            return new OpenBlockSlotInput(BlockType, ItemSlotCount, new MachineBlockSlotDetail(machine));
        }

        private static bool IsFixedInletBlockType(string blockType)
        {
            return blockType == BlockMasterElement.BlockTypeConst.RocketLaunchPad || blockType == BlockMasterElement.BlockTypeConst.SpaceElevatorBase;
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
