using System.Threading;
using Client.Common;
using Client.Common.Server;
using Client.Game.InGame.Context;
using Client.Game.InGame.Entity;
using Client.Game.InGame.SoundEffect;
using Client.Network.API;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using MessagePack;
using Server.Event.EventReceive;
using UnityEngine;
using VContainer.Unity;

namespace Client.Game.InGame.Chunk
{
    /// <summary>
    ///     サーバーからのパケットを受け取り、Viewにブロックの更新情報を渡す
    ///     IInitializableがないとDIコンテナ作成時にインスタンスが生成されないので実装しています
    /// </summary>
    public class ChunkDataHandler : IInitializable
    {
        private readonly BlockGameObjectDataStore _blockGameObjectDataStore;
        private readonly EntityObjectDatastore _entitiesDatastore;
        
        public ChunkDataHandler(BlockGameObjectDataStore blockGameObjectDataStore, EntityObjectDatastore entitiesDatastore, InitialHandshakeResponse initialHandshakeResponse)
        {
            _blockGameObjectDataStore = blockGameObjectDataStore;
            _entitiesDatastore = entitiesDatastore;
            //イベントをサブスクライブする
            ClientContext.VanillaApi.Event.RegisterEventResponse(PlaceBlockEventPacket.EventTag, OnBlockUpdate);
            ClientContext.VanillaApi.Event.RegisterEventResponse(RemoveBlockToSetEventPacket.EventTag, OnBlockRemove);
            
            //初期ハンドシェイクのデータを適用する
            foreach (var chunk in initialHandshakeResponse.Chunks) ApplyChunkData(chunk);
        }
        
        public void Initialize()
        {
            OnChunkUpdate().Forget();
        }
        
        /// <summary>
        ///     単一のブロックの更新イベント
        /// </summary>
        private void OnBlockUpdate(byte[] payload)
        {
            var data = MessagePackSerializer.Deserialize<PlaceBlockEventMessagePack>(payload);
            
            var blockPos = (Vector3Int)data.BlockData.BlockPos;
            var blockId = data.BlockData.BlockId;
            var blockDirection = data.BlockData.BlockDirection;
            
            //viewにブロックがおかれたことを通知する
            PlaceBlock(blockPos, blockId, blockDirection);
        }
        
        private void OnBlockRemove(byte[] packet)
        {
            var data = MessagePackSerializer.Deserialize<RemoveBlockEventMessagePack>(packet);
            
            //viewにブロックがおかれたことを通知する
            SoundEffectManager.Instance.PlaySoundEffect(SoundEffectType.DestroyBlock);
            _blockGameObjectDataStore.RemoveBlock(data.Position);
        }
        
        /// <summary>
        ///     0.5秒に1回のチャンクの更新イベント
        /// </summary>
        private async UniTask OnChunkUpdate()
        {
            var ct = new CancellationTokenSource().Token;
            
            while (true)
            {
                await GetChunkAndApply();
                await UniTask.Delay(NetworkConst.UpdateIntervalMilliseconds, cancellationToken: ct); //TODO 本当に0.5秒に1回でいいのか？
            }
            
            #region Internal
            
            async UniTask GetChunkAndApply()
            {
                var data = await ClientContext.VanillaApi.Response.GetChunkInfos(ct);
                if (data == null) return;
                
                foreach (var chunk in data) ApplyChunkData(chunk);
            }
            
            #endregion
        }
        
        
        private void ApplyChunkData(ChunkResponse chunk)
        {
            foreach (var block in chunk.Blocks) PlaceBlock(block.BlockPos, block.BlockId, block.BlockDirection);
            
            if (chunk.Entities == null)
            {
                Debug.Log("chunk.Entities is null");
                return;
            }
            
            _entitiesDatastore.OnEntitiesUpdate(chunk.Entities);
        }
        
        private void PlaceBlock(Vector3Int position, int id, BlockDirection blockDirection)
        {
            if (id == BlockConstant.NullBlockId)
            {
                _blockGameObjectDataStore.RemoveBlock(position);
                return;
            }
            
            _blockGameObjectDataStore.PlaceBlock(position, id, blockDirection);
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
}