using Game.Block.Interface;
using Game.World.Interface.DataStore;
using UnityEngine;

namespace Game.Block
{
    /// <summary>
    /// ブロック削除処理を担当するサービス
    /// Service handling block removal
    /// </summary>
    public class BlockRemover : IBlockRemover
    {
        private readonly IWorldBlockDatastore _worldBlockDatastore;

        public BlockRemover(IWorldBlockDatastore worldBlockDatastore)
        {
            _worldBlockDatastore = worldBlockDatastore;
        }

        public void RemoveBlock(BlockPositionInfo position, BlockRemovalType removalType)
        {
            // 削除理由をログに記録する
            // Log removal reason
            Debug.Log($"Block removal: Position={position.OriginalPos}, Type={removalType}");

            // ワールドデータストアに削除を委譲する
            // Delegate removal to world datastore
            _worldBlockDatastore.RemoveBlock(position.OriginalPos);
        }
    }
}
