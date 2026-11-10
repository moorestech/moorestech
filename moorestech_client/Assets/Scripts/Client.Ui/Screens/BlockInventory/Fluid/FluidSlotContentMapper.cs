using Client.Ui.Components.Slots;
using Client.Ui.Content;
using Client.Ui.State.BlockInventory.Logic.Common;
using Client.Ui.State.BlockInventory.Logic.Machine;

namespace Client.Ui.Screens.BlockInventory.Fluid
{
    /// <summary>
    /// 液体の列の 1 件を液体の枠の中身へ写す（C4-a の約束: 中身あり→Filled、空でゴーストあり→Ghost、他→Empty）
    /// Maps one fluid entry onto fluid-slot content (C4-a contract: filled→Filled, empty with ghost→Ghost, else Empty)
    /// </summary>
    public static class FluidSlotContentMapper
    {
        public static FluidSlotContent Map(BlockFluidSlotState slot, GameContentPresentation content)
        {
            if (slot is FilledBlockFluidSlotState filled) return FluidSlotContent.Filled(content.Fluid(filled.FluidId), filled.Amount, filled.Capacity, content.FluidFace(filled.FluidId));
            return FluidSlotContent.Empty;
        }

        public static FluidSlotContent MapWithGhost(BlockFluidSlotState slot, BoundFluidSlot bound, GameContentPresentation content)
        {
            // 中身ありにゴーストを出さない
            // Never show a ghost over real contents
            if (slot is FilledBlockFluidSlotState) return Map(slot, content);
            if (bound is GhostedFluidSlot ghosted) return FluidSlotContent.Ghost(content.Fluid(ghosted.Ghost.FluidId), ghosted.Ghost.Amount);
            return FluidSlotContent.Empty;
        }
    }
}
