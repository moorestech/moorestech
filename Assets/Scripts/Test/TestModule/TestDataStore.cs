using System.Collections.Generic;
using MainGame.Constant;
using MainGame.GameLogic.Interface;
using MainGame.Network.Interface;
using UnityEngine;

namespace Test.TestModule
{
    public class TestDataStore
    {
        public readonly Dictionary<Vector2Int, int[,]> Data = new Dictionary<Vector2Int, int[,]>();
        
        public void OnUpdateChunk(Vector2Int chunkPosition, int[,] ids)
        {
            Data.Add(chunkPosition, ids);
        }

        public void OnUpdateBlock(Vector2Int blockPosition, int id)
        {
            var chunkPos = ChunkConstant.BlockPositionToChunkOriginPosition(blockPosition);
            if (!Data.ContainsKey(chunkPos))
            {
                Data.Add(chunkPos, new int[ChunkConstant.ChunkSize, ChunkConstant.ChunkSize]);
            }
            Data[chunkPos][blockPosition.x - chunkPos.x, blockPosition.y - chunkPos.y] = id;
        }
    }
}