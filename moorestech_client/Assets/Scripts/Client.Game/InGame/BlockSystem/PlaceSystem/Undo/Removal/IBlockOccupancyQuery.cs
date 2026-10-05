using Core.Master;
using Game.Block.Interface;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Undo.Removal
{
    /// <summary>
    ///     再設置前の占有判定だけを公開する読み取り口
    ///     Read-only outlet exposing only the occupancy check before re-placing
    /// </summary>
    public interface IBlockOccupancyQuery
    {
        BlockFootprintOccupancy GetOccupancy(BlockPositionInfo target, BlockId blockId);
    }

    public enum BlockFootprintOccupancy
    {
        Free,
        SameBlockPresent,
        OtherBlock,
    }
}
