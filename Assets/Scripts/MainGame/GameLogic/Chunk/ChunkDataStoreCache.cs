using System.Collections.Generic;
using MainGame.Constant;
using MainGame.Network.Interface.Receive;
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
        private readonly Dictionary<Vector2Int, int[,]> _chunk = new Dictionary<Vector2Int, int[,]>();
        public ChunkDataStoreCache()
        {
            //イベントをサブスクライブする
        }

        public int GetBlock(Vector2Int blockPos)
        {
            var chunk = ChunkConstant.BlockPositionToChunkOriginPosition(blockPos);
            
            if (!_chunk.ContainsKey(chunk)) return BlockConstant.NullBlockId;
            
            var pos = GetBlockArrayIndex(chunk, blockPos);
            return _chunk[chunk][pos.x, pos.y];

        }
        
        
        /// <summary>
        /// チャンクの更新イベント
        /// </summary>
        private void OnChunkUpdate(OnChunkUpdateEventProperties properties)
        {
            if (_chunk.ContainsKey(properties.ChunkPos))
            {
                //チャンクのアップデートを発火させる
                return;
            }
            _chunk.Add(properties.ChunkPos, properties.BlockIds);
            //TODO viewにブロックがおかれたことを通知する
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
            //TODO viewにブロックがおかれたことを通知する
        }

        private Vector2Int GetBlockArrayIndex(Vector2Int chunkPos, Vector2Int blockPos)
        {
            var (x, y) = (
                GetBlockArrayIndex(chunkPos.x, blockPos.x),
                GetBlockArrayIndex(chunkPos.y, blockPos.y));
            return new Vector2Int(x, y);
        }
        
        private int GetBlockArrayIndex(int chunkPos, int blockPos)
        {
            if ( 0 <= chunkPos) return blockPos - chunkPos;
            return (-chunkPos) - (-blockPos);
        }

        public void Initialize() { }
    }
}