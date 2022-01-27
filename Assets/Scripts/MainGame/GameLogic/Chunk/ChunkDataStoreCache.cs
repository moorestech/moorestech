using System.Collections.Generic;
using MainGame.Constant;
using MainGame.Network.Interface;
using MainGame.Network.Interface.Receive;
using UnityEngine;
using VContainer.Unity;

namespace MainGame.GameLogic.Chunk
{
    //IInitializableがないとDIコンテナ作成時にインスタンスが生成されないので実装しておく
    public class ChunkDataStoreCache : IInitializable
    { 
        private readonly Dictionary<Vector2Int, int[,]> _chunk = new Dictionary<Vector2Int, int[,]>();
        public ChunkDataStoreCache(IChunkUpdateEvent chunkUpdateEvent)
        {
            //イベントをサブスクライブする
            chunkUpdateEvent.Subscribe(OnChunkUpdate,OnBlockUpdate);
        }

        private void OnChunkUpdate(OnChunkUpdateEventProperties properties)
        {
            if (_chunk.ContainsKey(properties.ChunkPos))
            {
                _chunk[properties.ChunkPos] = properties.BlockIds;
                return;
            }
            _chunk.Add(properties.ChunkPos, properties.BlockIds);
        }
        private void OnBlockUpdate(OnBlockUpdateEventProperties properties)
        {
            var blockPos = properties.BlockPos;
            var chunkPos = ChunkConstant.BlockPositionToChunkOriginPosition(blockPos);
            
            if (!_chunk.ContainsKey(chunkPos)) return;
            
            var (i, j) = (
                GetBlockArrayIndex(chunkPos.x, blockPos.x),
                GetBlockArrayIndex(chunkPos.y, blockPos.y));
            _chunk[chunkPos][i, j] = properties.BlockId;
        }

        private int GetBlockArrayIndex(int chunkPos, int blockPos)
        {
            if ( 0 <= chunkPos) return blockPos - chunkPos;
            return (-chunkPos) - (-blockPos);
        }

        public void Initialize() { }
    }
}