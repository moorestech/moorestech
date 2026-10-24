using Client.Common;
using Client.Game.InGame.Block;
using Client.Game.InGame.Context;
using Client.Game.InGame.SoundEffect;
using Client.Network.API;
using Core.Master;
using Game.Block.Interface;
using MessagePack;
using Server.Event.EventReceive;
using UnityEngine;

namespace Client.Game.InGame.World
{
    /// <summary>
    ///     ハンドシェイクの全量と設置・撤去イベントを受け取り、Viewにブロックの増減を渡す
    ///     Receives the handshake full state and the place/remove events, and hands block additions and removals to the view
    /// </summary>
    public class WorldDataHandler
    {
        private readonly BlockGameObjectDataStore _blockGameObjectDataStore;

        public WorldDataHandler(BlockGameObjectDataStore blockGameObjectDataStore, InitialHandshakeResponse initialHandshakeResponse)
        {
            _blockGameObjectDataStore = blockGameObjectDataStore;
            //イベントをサブスクライブする
            ClientContext.VanillaApi.Event.SubscribeEventResponse(PlaceBlockEventPacket.EventTag, OnBlockUpdate);
            ClientContext.VanillaApi.Event.SubscribeEventResponse(RemoveBlockToSetEventPacket.EventTag, OnBlockRemove);

            ApplyWorldData(initialHandshakeResponse.WorldData);
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
            PlaceBlock(block.BlockPos, block.BlockId, block.BlockDirection, block.BlockInstanceId, true);
        }

        private void OnBlockRemove(byte[] packet)
        {
            var data = MessagePackSerializer.Deserialize<RemoveBlockEventMessagePack>(packet);

            //viewにブロックがおかれたことを通知する
            SoundEffectManager.Instance.PlaySoundEffect(SoundEffectType.DestroyBlock);
            _blockGameObjectDataStore.RemoveBlock(data.Position);
        }

        private void ApplyWorldData(WorldDataResponse worldData)
        {
            foreach (var block in worldData.Blocks)
            {
                // ワールド同期では大量ブロックの初期配置アニメーションを抑止する
                // Suppress initial placement animation for bulk world synchronization
                PlaceBlock(block.BlockPos, block.BlockId, block.BlockDirection, block.BlockInstanceId, false);
            }
        }

        private void PlaceBlock(Vector3Int position, BlockId id, BlockDirection blockDirection, BlockInstanceId blockInstanceId, bool playPlaceAnimation)
        {
            if (id == BlockConstant.NullBlockId)
            {
                _blockGameObjectDataStore.RemoveBlock(position);
                return;
            }

            _blockGameObjectDataStore.PlaceBlock(position, id, blockDirection, blockInstanceId, playPlaceAnimation);
        }
    }
}
