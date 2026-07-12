using System.Collections.Generic;
using Game.Block.Interface;
using Game.Context;
using Game.World.Interface.DataStore;

namespace Game.World.DataStore
{
    /// <summary>
    ///     tick中に予約されたブロック破壊をtick末尾に一括反映するサービス。
    ///     ブロック本体だけ破壊済みでトポロジが旧セグメントに残る中間状態を作らないため、反映は必ずtick末尾のみで行う。
    ///     Service applying block removals reserved mid-tick in one batch at tick end.
    ///     Application happens only at tick end so no half state exists where the block body is gone but the topology still holds it.
    /// </summary>
    public class BlockRemovalReservationService : IBlockRemovalReservationService
    {
        private readonly List<(BlockInstanceId blockInstanceId, BlockRemoveReason reason)> _reservedRemovals = new();

        public void ReserveRemoval(BlockInstanceId blockInstanceId, BlockRemoveReason reason)
        {
            _reservedRemovals.Add((blockInstanceId, reason));
        }

        public void ApplyReservedRemovals()
        {
            if (_reservedRemovals.Count == 0) return;

            // FIFOで破壊を反映。既に消えているブロックはRemoveBlock側が無視する
            // Apply removals FIFO; RemoveBlock itself ignores blocks that are already gone
            foreach (var (blockInstanceId, reason) in _reservedRemovals)
            {
                ServerContext.WorldBlockDatastore.RemoveBlock(blockInstanceId, reason);
            }
            _reservedRemovals.Clear();
        }
    }
}
