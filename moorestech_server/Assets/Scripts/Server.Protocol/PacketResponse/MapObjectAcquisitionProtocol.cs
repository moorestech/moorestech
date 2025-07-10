using System;
using System.Collections.Generic;
using Game.Context;
using Game.Map.Interface.MapObject;
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
        
        public MapObjectAcquisitionProtocol(ServiceProvider serviceProvider)
        {
            _playerInventoryDataStore = serviceProvider.GetService<IPlayerInventoryDataStore>();
            _mapObjectUpdateEventPacket = serviceProvider.GetService<MapObjectUpdateEventPacket>();
        }
        
        
        public ProtocolMessagePackBase GetResponse(List<byte> payload)
        {
            var data = MessagePackSerializer.Deserialize<GetMapObjectProtocolProtocolMessagePack>(payload.ToArray());
            
            var mapObject = ServerContext.MapObjectDatastore.Get(data.InstanceId);
            var playerMainInventory = _playerInventoryDataStore.GetInventoryData(data.PlayerId).MainOpenableInventory;
            
            var earnedItem = mapObject.Attack(data.AttackDamage); // ダメージを与える
            
            // HP更新イベントを送信（破壊されていない場合のみ）
            if (!mapObject.IsDestroyed)
            {
                _mapObjectUpdateEventPacket.SendHpUpdateEvent(mapObject);
            }
            
            foreach (var earnItem in earnedItem) playerMainInventory.InsertItem(earnItem);
            
            return null;
        }
        
        [MessagePackObject]
        public class GetMapObjectProtocolProtocolMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public int PlayerId { get; set; }
            [Key(3)] public int InstanceId { get; set; }
            [Key(4)] public int AttackDamage { get; set; }
            
            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public GetMapObjectProtocolProtocolMessagePack() { }
            
            public GetMapObjectProtocolProtocolMessagePack(int playerId, int instanceId, int attackDamage)
            {
                Tag = ProtocolTag;
                PlayerId = playerId;
                InstanceId = instanceId;
                AttackDamage = attackDamage;
            }
        }
    }
}