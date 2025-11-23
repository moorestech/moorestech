using System;
using Game.Block.Interface;
using Game.World.Interface.DataStore;
using UnityEngine;

namespace Game.Block
{
    public class BlockRemover : IBlockRemover
    {
        private readonly IWorldBlockDatastore _worldBlockDatastore;

        public BlockRemover(IWorldBlockDatastore worldBlockDatastore)
        {
            _worldBlockDatastore = worldBlockDatastore;
        }

        public void Remove(BlockInstanceId blockInstanceId, BlockRemoveReason reason)
        {
            try
            {
                var block = _worldBlockDatastore.GetBlock(blockInstanceId);
                if (block == null) return;

                var pos = _worldBlockDatastore.GetBlockPosition(blockInstanceId);
                _worldBlockDatastore.RemoveBlock(pos);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Failed to remove block {blockInstanceId}: {e.Message}");
            }
        }
    }
}

