using System.Collections.Generic;
using MainGame.Basic;
using MainGame.Network.Event;
using MainGame.UnityView.Chunk;
using UnityEngine;
using VContainer.Unity;

namespace MainGame.GameLogic.Chunk
{
    /// <summary>
    /// サーバーからのパケットを受け取り、Viewにブロックの更新情報を渡す
    /// IInitializableがないとDIコンテナ作成時にインスタンスが生成されないので実装しています
    /// </summary>
    public class ChunkDataStoreCache : IInitializable
    {
        private readonly ChunkBlockGameObjectDataStore _chunkBlockGameObjectDataStore;
        private readonly Dictionary<Vector2Int, int[,]> _chunk = new();
        public ChunkDataStoreCache(NetworkReceivedChunkDataEvent networkReceivedChunkDataEvent,ChunkBlockGameObjectDataStore chunkBlockGameObjectDataStore)
        {
            _chunkBlockGameObjectDataStore = chunkBlockGameObjectDataStore;
            //イベントをサブスクライブする
            networkReceivedChunkDataEvent.Subscribe(OnChunkUpdate,OnBlockUpdate);
        }
        
        /// <summary>
        /// チャンクの更新イベント
        /// </summary>
        private void OnChunkUpdate(OnChunkUpdateEventProperties properties)
        {
            var chunkPos = properties.ChunkPos;
            //チャンクの情報を追加か更新
            if (_chunk.ContainsKey(chunkPos))
            {
                _chunk[chunkPos] = properties.BlockIds;
            }
            else
            {
                _chunk.Add(chunkPos, properties.BlockIds);
            }
            
            //viewにブロックがおかれたことを通知する
            for (int i = 0; i < ChunkConstant.ChunkSize; i++)
            {
                for (int j = 0; j < ChunkConstant.ChunkSize; j++)
                {
                    ViewReflectBlock(chunkPos + new Vector2Int(i,j),properties.BlockIds[i,j]);
                }
            }
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
            
            //viewにブロックがおかれたことを通知する
            ViewReflectBlock(blockPos, properties.BlockId);
        }

        private void ViewReflectBlock(Vector2Int position,int id)
        {
            if (id == BlockConstant.NullBlockId)
            {
                _chunkBlockGameObjectDataStore.GameObjectBlockRemove(position);
                return;
            }
            _chunkBlockGameObjectDataStore.GameObjectBlockPlace(position,id);
        }


        /// <summary>
        /// ブロックの座標とチャンクの座標から、IDの配列のインデックスを取得する
        /// </summary>
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