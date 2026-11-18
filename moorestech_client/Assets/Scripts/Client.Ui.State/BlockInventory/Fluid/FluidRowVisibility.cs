namespace Client.Ui.State.BlockInventory.Fluid
{
    /// <summary>
    /// 液体の行を出すか。機械は本体の中に液体の行を持つので出さず、機械でなく 1 件以上あるときだけ出す
    /// Whether to show the fluid row; machines carry fluids inside their body, so only non-machines with one or more slots show it
    /// </summary>
    public static class FluidRowVisibility
    {
        public static bool Shows(BlockFluidSlotsViewModel fluids, bool isMachine)
        {
            return !isMachine && 0 < fluids.Slots.Count;
        }
    }
}
