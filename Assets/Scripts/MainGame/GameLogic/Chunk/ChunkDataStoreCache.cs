using System.Collections.Generic;
using MainGame.Constant;
using MainGame.Network.Interface;
using MainGame.Network.Interface.Receive;
using MainGame.UnityView.Interface;
using UnityEngine;
using VContainer.Unity;

namespace MainGame.GameLogic.Chunk
{
    /// <summary>
    /// サーバーからのパケットを受け取り、Viewにブロックの更新情報を渡す
    /// </summary>
    //IInitializableがないとDIコンテナ作成時にインスタンスが生成されないので実装しておく
    public class ChunkDataStoreCache : IInitializable
    {
        private readonly BlockUpdateEvent _blockUpdateEvent;
        private readonly Dictionary<Vector2Int, int[,]> _chunk = new Dictionary<Vector2Int, int[,]>();
        public ChunkDataStoreCache(IChunkUpdateEvent chunkUpdateEvent,IBlockUpdateEvent blockUpdateEvent)
        {
            _blockUpdateEvent = blockUpdateEvent as BlockUpdateEvent;
            //イベントをサブスクライブする
            chunkUpdateEvent.Subscribe(OnChunkUpdate,OnBlockUpdate);
        }

        /// <summary>
        /// チャンクの更新イベント
        /// </summary>
        private void OnChunkUpdate(OnChunkUpdateEventProperties properties)
        {
            if (_chunk.ContainsKey(properties.ChunkPos))
            {
                //チャンクのアップデートを発火させる
                _blockUpdateEvent.DiffChunkUpdate(
                    properties.ChunkPos, properties.BlockIds,_chunk[properties.ChunkPos]);
                _chunk[properties.ChunkPos] = properties.BlockIds;
                return;
            }
            _chunk.Add(properties.ChunkPos, properties.BlockIds);
            _blockUpdateEvent.DiffChunkUpdate(properties.ChunkPos, properties.BlockIds);
        }

        /// <summary>
        /// 単一のブロックの更新イベント
        /// </summary>
        private void OnBlockUpdate(OnBlockUpdateEventProperties properties)
        {
            var blockPos = properties.BlockPos;
            var chunkPos = ChunkConstant.BlockPositionToChunkOriginPosition(blockPos);
            
            if (!_chunk.ContainsKey(chunkPos)) return;
            
            //ブロックを置き換え
            var (i, j) = (
                GetBlockArrayIndex(chunkPos.x, blockPos.x),
                GetBlockArrayIndex(chunkPos.y, blockPos.y));
            _chunk[chunkPos][i, j] = properties.BlockId;
            
            //ブロックの更新イベントを発火する
            _blockUpdateEvent.OnBlockUpdate(blockPos,properties.BlockId);
        }

        private int GetBlockArrayIndex(int chunkPos, int blockPos)
        {
            if ( 0 <= chunkPos) return blockPos - chunkPos;
            return (-chunkPos) - (-blockPos);
        }

        public void Initialize() { }
    }
}