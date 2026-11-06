using Core.Master;

namespace Client.Ui.State.BlockInventory.Logic.Common
{
    /// <summary>
    /// 液体スロットの中身（Web の FluidSlotData の kind "empty"/"filled" を sealed 派生で写す。判断記録 D-5）。部品 API の FluidSlotContent とは別物
    /// A fluid slot's content (Web FluidSlotData kinds "empty"/"filled" as sealed subtypes, D-5); distinct from the component API's FluidSlotContent
    /// </summary>
    public abstract class BlockFluidSlotState
    {
        public double Capacity { get; }

        protected BlockFluidSlotState(double capacity)
        {
            Capacity = capacity;
        }

        // 中身が入っていて量が正のときだけ実物（Web の hasRealFluid）
        // Real only when filled with a positive amount (Web hasRealFluid)
        public abstract bool HasRealFluid { get; }

        public static BlockFluidSlotState Empty(double capacity)
        {
            return new EmptyBlockFluidSlotState(capacity);
        }

        public static BlockFluidSlotState Filled(FluidId fluidId, double amount, double capacity)
        {
            return new FilledBlockFluidSlotState(fluidId, amount, capacity);
        }
    }

    public sealed class EmptyBlockFluidSlotState : BlockFluidSlotState
    {
        public EmptyBlockFluidSlotState(double capacity) : base(capacity)
        {
        }

        public override bool HasRealFluid => false;
    }

    public sealed class FilledBlockFluidSlotState : BlockFluidSlotState
    {
        public FluidId FluidId { get; }
        public double Amount { get; }

        public FilledBlockFluidSlotState(FluidId fluidId, double amount, double capacity) : base(capacity)
        {
            FluidId = fluidId;
            Amount = amount;
        }

        public override bool HasRealFluid => Amount > 0;
    }
}
