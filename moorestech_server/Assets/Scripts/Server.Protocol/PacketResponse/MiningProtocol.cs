using System;
using System.Collections.Generic;
using Core.Item.Interface;
using Game.Context;
using Game.Map;
using Game.PlayerInventory.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Event.EventReceive;
using Server.Util.MessagePack;
using UnityEngine;

namespace Server.Protocol.PacketResponse
{
    /// <summary>
    ///     手採採掘を対象別に分岐
    ///     Dispatch hand mining by target
    /// </summary>
    public class MiningProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:mining";

        private readonly IPlayerInventoryDataStore _playerInventoryDataStore;
        private readonly MapObjectUpdateEventPacket _mapObjectUpdateEventPacket;
        private readonly MapObjectMiningService _mapObjectMiningService;
        private readonly VeinHandMiningService _veinHandMiningService;

        public MiningProtocol(ServiceProvider serviceProvider)
        {
            _playerInventoryDataStore = serviceProvider.GetService<IPlayerInventoryDataStore>();
            _mapObjectUpdateEventPacket = serviceProvider.GetService<MapObjectUpdateEventPacket>();
            _mapObjectMiningService = serviceProvider.GetService<MapObjectMiningService>();
            _veinHandMiningService = serviceProvider.GetService<VeinHandMiningService>();
        }

        public ProtocolMessagePackBase GetResponse(byte[] payload, PacketResponseContext context)
        {
            var data = MessagePackSerializer.Deserialize<MiningProtocolMessagePack>(payload);
            var playerInventory = _playerInventoryDataStore.GetInventoryData(data.PlayerId);
            var equippedItem = playerInventory.EquipmentInventory.GetSelectedItem();

            // 対象別サービスへ委譲
            // Delegate to target service
            var earnedItems = data.TargetType switch
            {
                MiningTargetType.MapObject => MineMapObject(),
                MiningTargetType.Vein => MineVein(),
                _ => throw new ArgumentOutOfRangeException(nameof(data.TargetType), data.TargetType, null),
            };

            if (earnedItems == null) return null;

            // 成功報酬だけ追加
            // Add only successful rewards
            foreach (var earnItem in earnedItems) playerInventory.MainOpenableInventory.InsertItem(earnItem);
            return null;

            #region Internal

            List<IItemStack> MineMapObject()
            {
                var mapObject = ServerContext.MapObjectDatastore.Get(data.InstanceId);
                var result = _mapObjectMiningService.TryAttack(data.PlayerId, mapObject, equippedItem, out var items);
                if (result != MiningAttackResult.Success)
                {
                    Debug.Log($"Mining attack rejected. playerId:{data.PlayerId} instanceId:{data.InstanceId} result:{result}");
                    return null;
                }

                // 未破壊時だけHP更新
                // Update HP only when intact
                if (!mapObject.IsDestroyed) _mapObjectUpdateEventPacket.SendHpUpdateEvent(mapObject);
                return items;
            }

            List<IItemStack> MineVein()
            {
                var result = _veinHandMiningService.TryMine(data.PlayerId, data.VeinPosition.Vector3Int, equippedItem, out var items);
                if (result != VeinMiningResult.Success)
                {
                    Debug.Log($"Vein mining rejected. playerId:{data.PlayerId} position:{data.VeinPosition.Vector3Int} result:{result}");
                    return null;
                }

                // veinは無限資源で状態を持たないため更新イベントを送らない
                // Veins are stateless infinite resources, so no update event is sent
                return items;
            }

            #endregion
        }

        public enum MiningTargetType
        {
            MapObject,
            Vein,
        }

        [MessagePackObject]
        public class MiningProtocolMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public int PlayerId { get; set; }
            [Key(3)] public MiningTargetType TargetType { get; set; }
            [Key(4)] public int InstanceId { get; set; }
            [Key(5)] public Vector3IntMessagePack VeinPosition { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public MiningProtocolMessagePack() { }

            private MiningProtocolMessagePack(int playerId, MiningTargetType targetType, int instanceId, Vector3IntMessagePack veinPosition)
            {
                Tag = ProtocolTag;
                PlayerId = playerId;
                TargetType = targetType;
                InstanceId = instanceId;
                VeinPosition = veinPosition;
            }

            public static MiningProtocolMessagePack CreateMapObjectRequest(int playerId, int instanceId)
            {
                return new MiningProtocolMessagePack(playerId, MiningTargetType.MapObject, instanceId, new Vector3IntMessagePack(Vector3Int.zero));
            }

            public static MiningProtocolMessagePack CreateVeinRequest(int playerId, Vector3Int position)
            {
                return new MiningProtocolMessagePack(playerId, MiningTargetType.Vein, 0, new Vector3IntMessagePack(position));
            }
        }
    }
}
