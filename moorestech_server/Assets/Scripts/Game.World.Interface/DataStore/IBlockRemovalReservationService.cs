using Game.Block.Interface;

namespace Game.World.Interface.DataStore
{
    /// <summary>
    ///     tick中の計算で決定したブロック破壊の予約窓口。
    ///     予約されたブロックはそのtickの計算に最後まで参加し、tick末尾のApplyReservedRemovalsで一括破壊される。
    ///     Reservation gateway for block destruction decided by in-tick computation.
    ///     A reserved block keeps participating in this tick's calculations and is destroyed in batch by ApplyReservedRemovals at tick end.
    /// </summary>
    public interface IBlockRemovalReservationService
    {
        void ReserveRemoval(BlockInstanceId blockInstanceId, BlockRemoveReason reason);
        void ApplyReservedRemovals();
    }
}
