using System;
using System.Collections.Generic;
using Common.Debug;
using Core.Item.Interface;
using Game.Context;
using Game.Map;
using Game.PlayerInventory.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Event.EventReceive;

namespace Server.Protocol.PacketResponse
{
    /// <summary>
    ///     MapObjectを取得するときのプロトコル
    /// </summary>
    public class MapObjectAcquisitionProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:mapObjectInfoAcquisition";
        
        private readonly IPlayerInventoryDataStore _playerInventoryDataStore;
        private readonly MapObjectUpdateEventPacket _mapObjectUpdateEventPacket;
        private readonly MapObjectMiningService _mapObjectMiningService;

        public MapObjectAcquisitionProtocol(ServiceProvider serviceProvider)
        {
            _playerInventoryDataStore = serviceProvider.GetService<IPlayerInventoryDataStore>();
            _mapObjectUpdateEventPacket = serviceProvider.GetService<MapObjectUpdateEventPacket>();
            _mapObjectMiningService = serviceProvider.GetService<MapObjectMiningService>();
        }


        public ProtocolMessagePackBase GetResponse(byte[] payload, PacketResponseContext context)
        {
            var data = MessagePackSerializer.Deserialize<GetMapObjectProtocolProtocolMessagePack>(payload);

            var mapObject = ServerContext.MapObjectDatastore.Get(data.InstanceId);

            // 破壊済みへの打撃は何も起こさない。デバッグフラグ読みのファイルIOもここで打ち切る
            // A hit on a destroyed object does nothing; this also cuts off the debug flag file IO
            if (mapObject.IsDestroyed) return null;

            var playerInventory = _playerInventoryDataStore.GetInventoryData(data.PlayerId);
            var equippedItem = playerInventory.EquipmentInventory.GetSelectedItem();

            // ダメージ算出とクールダウン検証はサーバが握り、デバッグ高速採掘のときだけそれを飛ばす
            // The server owns damage and cooldown resolution; only the debug super-mine flag skips it
            List<IItemStack> earnedItems;
            if (DebugParameters.GetValueOrDefaultBool(DebugParameterKeys.MapObjectSuperMine))
            {
                if (!_mapObjectMiningService.ForceDestroy(mapObject, out earnedItems)) return null;
            }
            else
            {
                if (!_mapObjectMiningService.TryAttack(data.PlayerId, mapObject, equippedItem, out earnedItems)) return null;
            }

            // HP更新イベントを送信（破壊されていない場合のみ）
            if (!mapObject.IsDestroyed)
            {
                _mapObjectUpdateEventPacket.SendHpUpdateEvent(mapObject);
            }

            foreach (var earnItem in earnedItems) playerInventory.MainOpenableInventory.InsertItem(earnItem);

            return null;
        }
        
        [MessagePackObject]
        public class GetMapObjectProtocolProtocolMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public int PlayerId { get; set; }
            [Key(3)] public int InstanceId { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public GetMapObjectProtocolProtocolMessagePack() { }

            public GetMapObjectProtocolProtocolMessagePack(int playerId, int instanceId)
            {
                Tag = ProtocolTag;
                PlayerId = playerId;
                InstanceId = instanceId;
            }
        }
    }
}