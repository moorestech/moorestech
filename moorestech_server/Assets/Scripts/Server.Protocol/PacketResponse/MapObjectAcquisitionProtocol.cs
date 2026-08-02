using System;
using Game.Context;
using Game.Map;
using Game.PlayerInventory.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Event.EventReceive;
using UnityEngine;

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
            var playerInventory = _playerInventoryDataStore.GetInventoryData(data.PlayerId);
            var equippedItem = playerInventory.EquipmentInventory.GetSelectedItem();

            // ダメージ算出もクールダウン検証もデバッグバイパスもサーバのサービス側が握る
            // The server-side service owns damage resolution, cooldown validation, and the debug bypass
            var result = _mapObjectMiningService.TryAttack(data.PlayerId, mapObject, equippedItem, out var earnedItems);
            switch (result)
            {
                case MiningAttackResult.Success:
                    break;
                case MiningAttackResult.AlreadyDestroyed:
                case MiningAttackResult.NoTool:
                case MiningAttackResult.ToolMismatch:
                case MiningAttackResult.CooldownNotElapsed:
                    Debug.Log($"Mining attack rejected. playerId:{data.PlayerId} instanceId:{data.InstanceId} result:{result}");
                    return null;
                default:
                    throw new ArgumentOutOfRangeException(nameof(result), result, null);
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
