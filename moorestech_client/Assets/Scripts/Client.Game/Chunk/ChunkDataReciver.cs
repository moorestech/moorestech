using System.Collections.Generic;
using System.Threading;
using Client.Network.NewApi;
using Game.World.Interface.DataStore;
using Constant;
using Cysharp.Threading.Tasks;
using MainGame.Network.Event;
using MainGame.UnityView.Chunk;
using MessagePack;
using Server.Event.EventReceive;
using Server.Protocol.PacketResponse.Const;
using UnityEditor.Callbacks;
using UnityEngine;
using VContainer.Unity;

namespace MainGame.Presenter.Block
{
    /// <summary>
    ///     サーバーからのパケットを受け取り、Viewにブロックの更新情報を渡す
    ///     IInitializableがないとDIコンテナ作成時にインスタンスが生成されないので実装しています
    /// </summary>
    public class ChunkDataReciver : IInitializable
    {
        private readonly Dictionary<Vector2Int, BlockInfo[,]> _chunk = new();
        private readonly ChunkBlockGameObjectDataStore _chunkBlockGameObjectDataStore;

        public ChunkDataReciver(ChunkBlockGameObjectDataStore chunkBlockGameObjectDataStore)
        {
            _chunkBlockGameObjectDataStore = chunkBlockGameObjectDataStore;
            //イベントをサブスクライブする
            VanillaApi.RegisterEventResponse(PlaceBlockEventPacket.EventTag, OnBlockUpdate);
        }
        
        /// <summary>
        ///     単一のブロックの更新イベント
        /// </summary>
        private void OnBlockUpdate(byte[] payload)
        {
            var data = MessagePackSerializer.Deserialize<PlaceBlockEventMessagePack>(payload);
            
            var blockPos = data.BlockPos.Vector2Int;
            var chunkPos = ChunkConstant.BlockPositionToChunkOriginPosition(blockPos);

            if (!_chunk.ContainsKey(chunkPos)) return;

            //ブロックを置き換え
            var (i, j) = (
                GetBlockArrayIndex(chunkPos.x, blockPos.x),
                GetBlockArrayIndex(chunkPos.y, blockPos.y));
            _chunk[chunkPos][i, j] = new BlockInfo(data.BlockId,(BlockDirection)data.Direction);

            //viewにブロックがおかれたことを通知する
            PlaceOrRemoveBlock(blockPos, data.BlockId, (BlockDirection)data.Direction);
        }

        public void Initialize()
        {
            OnChunkUpdate().Forget();
        }

        /// <summary>
        /// 5秒に1回のチャンクの更新イベント
        /// </summary>
        private async UniTask OnChunkUpdate()
        {
            var ct = new CancellationTokenSource().Token;
            
            var chunks = new List<Vector2Int>();// TODO 動的にチャンクを取得するようにする？
            var getChunkSize = 5;
            for (var i = -getChunkSize; i <= getChunkSize; i++)
            for (var j = -getChunkSize; j <= getChunkSize; j++)
                chunks.Add(new Vector2Int(i * ChunkResponseConst.ChunkSize, j * ChunkResponseConst.ChunkSize));
            
            while (true)
            {
                await GetChunkAndApply();
                await UniTask.Delay(5000, cancellationToken: ct); //TODO 本当に5秒に1回でいいのか？
            }

            #region Internal

            async UniTask GetChunkAndApply()
            {
                var data = await VanillaApi.GetChunkInfos(chunks, ct);
                foreach (var chunk in data)
                {
                    var chunkPos = chunk.ChunkPos;
                    _chunk[chunkPos] = chunk.Blocks;
                    for (var i = 0; i < chunk.Blocks.GetLength(0); i++)
                    for (var j = 0; j < chunk.Blocks.GetLength(1); j++)
                    {
                        var blockPos = new Vector2Int(chunkPos.x + i, chunkPos.y + j);
                        var blockId = chunk.Blocks[i, j].BlockId;
                        var blockDirections = chunk.Blocks[i, j].BlockDirection;
                        PlaceOrRemoveBlock(blockPos, blockId, blockDirections);
                    }
                }
            }

            #endregion
        }


        private void PlaceOrRemoveBlock(Vector2Int position, int id, BlockDirection blockDirection)
        {
            if (id == BlockConstant.NullBlockId)
            {
                _chunkBlockGameObjectDataStore.GameObjectBlockRemove(position);
                return;
            }

            _chunkBlockGameObjectDataStore.GameObjectBlockPlace(position, id, blockDirection);
        }


        /// <summary>
        ///     ブロックの座標とチャンクの座標から、IDの配列のインデックスを取得する
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
            if (0 <= chunkPos) return blockPos - chunkPos;
            return -chunkPos - -blockPos;
        }
    }
    
    

    public class BlockInfo
    {
        public readonly BlockDirection BlockDirection;
        public readonly int BlockId;
        
        public BlockInfo(int blockId, BlockDirection blockDirection)
        {
            BlockId = blockId;
            BlockDirection = blockDirection;
        }
    }
}