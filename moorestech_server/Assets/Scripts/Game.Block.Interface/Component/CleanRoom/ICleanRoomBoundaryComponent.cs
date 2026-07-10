namespace Game.Block.Interface.Component
{
    // 密閉判定で境界（気密殻）として扱われるブロックの種別
    // Kind of block treated as an airtight boundary in sealed-room detection
    public enum CleanRoomBoundaryKind
    {
        Wall,
        Door,
        ItemHatch,
        PipeHatch,
    }

    /// <summary>
    ///     このコンポーネントを持つブロックの占有セルは flood-fill を遮る境界セルになる
    ///     Cells occupied by a block with this component block the flood-fill as boundary cells
    /// </summary>
    public interface ICleanRoomBoundaryComponent : IBlockComponent
    {
        CleanRoomBoundaryKind BoundaryKind { get; }
    }
}
