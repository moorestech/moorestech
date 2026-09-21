namespace Game.Block.Blocks.Machine.Inventory
{
    /// <summary>
    ///     機械スロットへの配置判定結果。拒否時はログで原因を区別できるよう理由ごとに値を分ける
    ///     Result of a machine slot placement check; rejections carry distinct reasons so logs can tell them apart
    /// </summary>
    public enum MachineSlotPlacementCheck
    {
        Allowed,
        RecipeNotSelected,
        ItemNotBoundToSlot,
        SlotBeyondRecipeOutputs,
    }
}
