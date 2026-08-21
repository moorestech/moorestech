using System;
using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;
using Game.Context;
using Game.Map;
using Game.PlayerInventory.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Event.EventReceive;
using Server.Event.Notification;
using Server.Util.MessagePack;
using UnityEngine;

namespace Server.Protocol.PacketResponse
{
    /// <summary>
    ///     手掘りを対象別に分岐
    ///     Dispatch hand mining by target
    /// </summary>
    public class MiningProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:mining";

        private readonly IPlayerInventoryDataStore _playerInventoryDataStore;
        private readonly MapObjectUpdateEventPacket _mapObjectUpdateEventPacket;
        private readonly MapObjectMiningService _mapObjectMiningService;
        private readonly VeinHandMiningService _veinHandMiningService;
        private readonly NotificationService _notificationService;

        public MiningProtocol(ServiceProvider serviceProvider)
        {
            _playerInventoryDataStore = serviceProvider.GetService<IPlayerInventoryDataStore>();
            _mapObjectUpdateEventPacket = serviceProvider.GetService<MapObjectUpdateEventPacket>();
            _mapObjectMiningService = serviceProvider.GetService<MapObjectMiningService>();
            _veinHandMiningService = serviceProvider.GetService<VeinHandMiningService>();
            _notificationService = serviceProvider.GetService<NotificationService>();
        }

        public ProtocolMessagePackBase GetResponse(byte[] payload, PacketResponseContext context)
        {
            var data = MessagePackSerializer.Deserialize<MiningProtocolMessagePack>(payload);
            var playerInventory = _playerInventoryDataStore.GetInventoryData(data.PlayerId);
            var equippedItem = playerInventory.EquipmentInventory.GetSelectedItem();

            var earnedItems = data.TargetType switch
            {
                MiningTargetType.MapObject => MineMapObject(),
                MiningTargetType.Vein => MineVein(),
                _ => throw new ArgumentOutOfRangeException(nameof(data.TargetType), data.TargetType, null),
            };

            if (earnedItems == null) return null;

            var insertion = InsertEarnedItems();
            NotifyEarnedItems(insertion.insertedCounts);
            NotifyLostEarnedItems(insertion.lostCount);
            return null;

            #region Internal

            // 空き検査を超えて生成されるので実挿入量と溢れ量を数える
            // Generation can outgrow the space check, so count what actually landed and what overflowed
            (Dictionary<ItemId, int> insertedCounts, int lostCount) InsertEarnedItems()
            {
                var insertedCounts = new Dictionary<ItemId, int>();
                var lostCount = 0;
                foreach (var earnItem in earnedItems)
                {
                    var remain = playerInventory.MainOpenableInventory.InsertItem(earnItem);
                    insertedCounts.TryGetValue(earnItem.Id, out var current);
                    insertedCounts[earnItem.Id] = current + earnItem.Count - remain.Count;
                    lostCount += remain.Count;
                }

                return (insertedCounts, lostCount);
            }

            // 同一アイテムの複数スタックを1本に畳む
            // Fold split stacks of the same item into a single notification per mining action
            void NotifyEarnedItems(Dictionary<ItemId, int> insertedCounts)
            {
                foreach (var insertedCount in insertedCounts)
                {
                    // 1個も入らなければ通知しない
                    // No notification when nothing landed
                    if (insertedCount.Value <= 0) continue;
                    _notificationService.NotifyWithoutCooldown(data.PlayerId, NotificationMessagePack.CreateItemEarned(insertedCount.Key, insertedCount.Value));
                }
            }

            // 満杯で失った分は無言にせず拒否通知に載せる。連打はクールダウンが畳む
            // Items lost to a full inventory are surfaced as a denial instead of staying silent; the cooldown folds bursts
            void NotifyLostEarnedItems(int lostCount)
            {
                if (lostCount <= 0) return;
                _notificationService.Notify(data.PlayerId, NotificationMessagePack.CreateOperationDenied("denied.miningInventoryFull", Array.Empty<string>()));
            }

            List<IItemStack> MineMapObject()
            {
                var mapObject = ServerContext.MapObjectDatastore.Get(data.InstanceId);
                var result = _mapObjectMiningService.TryAttack(data.PlayerId, mapObject, equippedItem, playerInventory.MainOpenableInventory, out var items);
                switch (result)
                {
                    case MiningAttackResult.Success:
                        break;
                    case MiningAttackResult.AlreadyDestroyed:
                    case MiningAttackResult.NoTool:
                    case MiningAttackResult.ToolMismatch:
                    case MiningAttackResult.CooldownNotElapsed:
                    case MiningAttackResult.InventoryFull:
                        Debug.Log($"Mining attack rejected. playerId:{data.PlayerId} instanceId:{data.InstanceId} result:{result}");
                        return null;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(result), result, null);
                }

                // 未破壊時だけHP更新
                // Update HP only when intact
                if (!mapObject.IsDestroyed) _mapObjectUpdateEventPacket.SendHpUpdateEvent(mapObject);
                return items;
            }

            List<IItemStack> MineVein()
            {
                var result = _veinHandMiningService.TryMine(data.PlayerId, data.VeinGuid, data.VeinPosition.Vector3Int, equippedItem, playerInventory.MainOpenableInventory, out var items);
                switch (result)
                {
                    case VeinMiningResult.Success:
                        break;
                    case VeinMiningResult.VeinNotFound:
                    case VeinMiningResult.VeinGuidMismatch:
                    case VeinMiningResult.HandMiningNotAllowed:
                    case VeinMiningResult.NoTool:
                    case VeinMiningResult.ToolMismatch:
                    case VeinMiningResult.CooldownNotElapsed:
                    case VeinMiningResult.InventoryFull:
                        Debug.Log($"Vein mining rejected. playerId:{data.PlayerId} veinGuid:{data.VeinGuid} position:{data.VeinPosition.Vector3Int} result:{result}");
                        return null;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(result), result, null);
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

            // 同座標に重なる別鉱脈を掘り分ける
            // Separates veins overlapping the same cell
            [Key(6)] public Guid VeinGuid { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public MiningProtocolMessagePack() { }

            private MiningProtocolMessagePack(int playerId, MiningTargetType targetType, int instanceId, Vector3IntMessagePack veinPosition, Guid veinGuid)
            {
                Tag = ProtocolTag;
                PlayerId = playerId;
                TargetType = targetType;
                InstanceId = instanceId;
                VeinPosition = veinPosition;
                VeinGuid = veinGuid;
            }

            public static MiningProtocolMessagePack CreateMapObjectRequest(int playerId, int instanceId)
            {
                return new MiningProtocolMessagePack(playerId, MiningTargetType.MapObject, instanceId, new Vector3IntMessagePack(Vector3Int.zero), Guid.Empty);
            }

            public static MiningProtocolMessagePack CreateVeinRequest(int playerId, Guid veinGuid, Vector3Int position)
            {
                return new MiningProtocolMessagePack(playerId, MiningTargetType.Vein, 0, new Vector3IntMessagePack(position), veinGuid);
            }
        }
    }
}
