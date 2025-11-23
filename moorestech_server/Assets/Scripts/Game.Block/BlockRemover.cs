using Game.Block.Interface;
using UnityEngine;

namespace Game.Block
{
    /// <summary>
    /// ブロック削除処理を担当するサービス
    /// Service handling block removal
    /// </summary>
    public class BlockRemover : IBlockRemover
    {
        private readonly System.Func<Game.World.Interface.DataStore.IWorldBlockDatastore> _worldBlockDatastoreGetter;

        public BlockRemover(System.Func<Game.World.Interface.DataStore.IWorldBlockDatastore> worldBlockDatastoreGetter)
        {
            _worldBlockDatastoreGetter = worldBlockDatastoreGetter;
        }

        public void RemoveBlock(BlockPositionInfo position, BlockRemovalType removalType)
        {
            // 削除理由をログに記録する
            // Log removal reason
            Debug.Log($"Block removal: Position={position.OriginalPos}, Type={removalType}");

            // ワールドデータストアに削除を委譲する
            // Delegate removal to world datastore
            _worldBlockDatastoreGetter().RemoveBlock(position.OriginalPos);
        }
    }
}
