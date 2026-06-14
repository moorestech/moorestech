namespace Game.Block.Interface.Component
{
    // 境界ブロックの種別。フェーズ3/5の汚染計算・I/Oで参照する。
    // Kind of boundary block; consumed by phase-3/5 pollution calc and I/O.
    public enum CleanRoomBoundaryKind
    {
        Wall,
        DoorHatch,
        ItemHatch,
        PipeHatch,
    }

    // クリーンルームの気密境界として機能するブロックが実装するマーカー。
    // Marker for blocks that act as an airtight boundary of a clean room.
    public interface ICleanRoomBoundaryComponent : IBlockComponent
    {
        CleanRoomBoundaryKind BoundaryKind { get; }
    }
}
