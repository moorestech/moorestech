using System.Collections.Generic;
using MainGame.Basic;
using MainGame.Network.Interface;
using MainGame.Network.Interface.Receive;
using UnityEngine;

namespace Test.TestModule
{
    public class TestChunkDataStore
    {
        public readonly Dictionary<Vector2Int, int[,]> Data = new Dictionary<Vector2Int, int[,]>();
        
        public void OnUpdateChunk(OnChunkUpdateEventProperties properties)
        {
            Data.Add(properties.ChunkPos, properties.BlockIds);
        }

        public void OnUpdateBlock(OnBlockUpdateEventProperties properties)
        {
            Vector2Int blockPosition = properties.BlockPos;
            int id = properties.BlockId;
            
            var chunkPos = ChunkConstant.BlockPositionToChunkOriginPosition(blockPosition);
            if (!Data.ContainsKey(chunkPos))
            {
                Data.Add(chunkPos, new int[ChunkConstant.ChunkSize, ChunkConstant.ChunkSize]);
            }
            Data[chunkPos][blockPosition.x - chunkPos.x, blockPosition.y - chunkPos.y] = id;
        }
    }
}