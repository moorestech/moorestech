using System.Threading;
using Client.Common;
using Client.Common.Server;
using Client.Game.InGame.Block;
using Client.Game.InGame.Context;
using Client.Game.InGame.Entity;
using Client.Game.InGame.SoundEffect;
using Client.Network.API;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using MessagePack;
using Server.Event.EventReceive;
using UnityEngine;
using VContainer.Unity;

namespace Client.Game.InGame.World
{
    /// <summary>
    ///     サーバーからのパケットを受け取り、Viewにブロックの更新情報を渡す
    ///     IInitializableがないとDIコンテナ作成時にインスタンスが生成されないので実装しています
    /// </summary>
    public class WorldDataHandler : IInitializable
    {
        private readonly BlockGameObjectDataStore _blockGameObjectDataStore;
        private readonly EntityObjectDatastore _entitiesDatastore;
        
        public WorldDataHandler(BlockGameObjectDataStore blockGameObjectDataStore, EntityObjectDatastore entitiesDatastore, InitialHandshakeResponse initialHandshakeResponse)
        {
            _blockGameObjectDataStore = blockGameObjectDataStore;
            _entitiesDatastore = entitiesDatastore;
            //イベントをサブスクライブする
            ClientContext.VanillaApi.Event.SubscribeEventResponse(PlaceBlockEventPacket.EventTag, OnBlockUpdate);
            ClientContext.VanillaApi.Event.SubscribeEventResponse(RemoveBlockToSetEventPacket.EventTag, OnBlockRemove);
            
            ApplyWorldData(initialHandshakeResponse.WorldData);
        }
        
        public void Initialize()
        {
            UpdateWorldData().Forget();
        }
        
        /// <summary>
        ///     単一のブロックの更新イベント
        ///     Single block update event
        /// </summary>
        private void OnBlockUpdate(byte[] payload)
        {
            var data = MessagePackSerializer.Deserialize<PlaceBlockEventMessagePack>(payload);
            
            // viewにブロックがおかれたことを通知する
            // Notify view that a block has been placed
            var block = data.BlockData;
            PlaceBlock(block.BlockPos, block.BlockId, block.BlockDirection, block.BlockInstanceId);
        }
        
        private void OnBlockRemove(byte[] packet)
        {
            var data = MessagePackSerializer.Deserialize<RemoveBlockEventMessagePack>(packet);
            
            //viewにブロックがおかれたことを通知する
            SoundEffectManager.Instance.PlaySoundEffect(SoundEffectType.DestroyBlock);
            _blockGameObjectDataStore.RemoveBlock(data.Position);
        }
        
        /// <summary>
        ///     0.5秒に1回のワールドの更新をリクエストし、適用する
        /// </summary>
        private async UniTask UpdateWorldData()
        {
            var ct = new CancellationTokenSource().Token;
            
            while (true)
            {
                await GetAndApplyWorldData();
                await UniTask.Delay(NetworkConst.UpdateIntervalMilliseconds, cancellationToken: ct); //TODO 本当に0.5秒に1回でいいのか？
            }
            
            #region Internal
            
            async UniTask GetAndApplyWorldData()
            {
                var data = await ClientContext.VanillaApi.Response.GetWorldData(ct);
                if (data == null) return;
                
                ApplyWorldData(data);
            }
            
            #endregion
        }
        
        
        private void ApplyWorldData(WorldDataResponse worldData)
        {
            foreach (var block in worldData.Blocks)
            {
                PlaceBlock(block.BlockPos, block.BlockId, block.BlockDirection, block.BlockInstanceId);
            }

            if (worldData.Entities == null)
            {
                return;
            }

            _entitiesDatastore.OnEntitiesUpdate(worldData.Entities);
        }

        private void PlaceBlock(Vector3Int position, BlockId id, BlockDirection blockDirection, BlockInstanceId blockInstanceId)
        {
            if (id == BlockConstant.NullBlockId)
            {
                _blockGameObjectDataStore.RemoveBlock(position);
                return;
            }

            _blockGameObjectDataStore.PlaceBlock(position, id, blockDirection, blockInstanceId);
        }
    }
}