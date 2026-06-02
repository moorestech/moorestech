using System;
using Core.Inventory;
using Game.PlayerInventory.Interface;
using Game.Train.Unit;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Protocol.PacketResponse.Util.InventoryMoveUtil;
using Server.Protocol.PacketResponse.Util.InventoryService;

namespace Server.Protocol.PacketResponse
{
    /// <summary>
    ///     インベントリを整理（同種スタック結合＋ItemId昇順に詰め直し）するプロトコルです
    ///     Protocol that tidies an inventory (merge same-item stacks and re-pack in ItemId order)
    /// </summary>
    public class SortInventoryProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:sortInventory";

        private readonly IPlayerInventoryDataStore _playerInventoryDataStore;
        private readonly ITrainUnitLookupDatastore _trainUnitLookupDatastore;

        public SortInventoryProtocol(ServiceProvider serviceProvider)
        {
            _playerInventoryDataStore = serviceProvider.GetService<IPlayerInventoryDataStore>();
            _trainUnitLookupDatastore = serviceProvider.GetService<ITrainUnitLookupDatastore>();
        }

        public ProtocolMessagePackBase GetResponse(byte[] payload, PacketResponseContext context)
        {
            var data = MessagePackSerializer.Deserialize<SortInventoryProtocolMessagePack>(payload);

            // 対象インベントリを解決（存在しなければ何もしない）
            // Resolve the target inventory; do nothing if it cannot be found.
            var inventory = OpenableInventoryResolver.Resolve(data.Target.InventoryType, data.PlayerId, data.Target.InventoryIdentifier, _playerInventoryDataStore, _trainUnitLookupDatastore);
            if (inventory == null) return null;

            // メインインベントリのときはホットバーを整理対象から除外する
            // Exclude the hotbar from sorting when the target is the main inventory.
            var excludeSlots = data.Target.InventoryType == ItemMoveInventoryType.MainInventory
                ? PlayerInventoryConst.HotBarSlots
                : Array.Empty<int>();

            InventorySortService.Sort(inventory, excludeSlots);

            return null;
        }

        #region MessagePack

        [MessagePackObject]
        public class SortInventoryProtocolMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public int PlayerId { get; set; }

            [Key(3)] public InventoryItemMoveProtocol.ItemMoveInventoryInfoMessagePack Target { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public SortInventoryProtocolMessagePack() { }

            public SortInventoryProtocolMessagePack(int playerId, ItemMoveInventoryInfo target)
            {
                Tag = ProtocolTag;
                PlayerId = playerId;
                Target = new InventoryItemMoveProtocol.ItemMoveInventoryInfoMessagePack(target, 0);
            }
        }

        #endregion
    }
}
