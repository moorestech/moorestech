using System;
using Game.PlayerInventory.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Protocol.PacketResponse.Util.InventoryService;
using Server.Util.MessagePack;

namespace Server.Protocol.PacketResponse
{
    /// <summary>
    ///     インベントリを整理（同種スタック結合＋ItemId昇順に詰め直し）するプロトコルです
    ///     Protocol that tidies an inventory (merge same-item stacks and re-pack in ItemId order)
    /// </summary>
    public class SortInventoryProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:sortInventory";

        private readonly OpenableInventoryResolver _openableInventoryResolver;

        public SortInventoryProtocol(ServiceProvider serviceProvider)
        {
            _openableInventoryResolver = serviceProvider.GetService<OpenableInventoryResolver>();
        }

        public ProtocolMessagePackBase GetResponse(byte[] payload, PacketResponseContext context)
        {
            var data = MessagePackSerializer.Deserialize<SortInventoryProtocolMessagePack>(payload);

            // 対象インベントリを解決（存在しなければ何もしない）
            // Resolve the target inventory; do nothing if it cannot be found.
            var inventory = _openableInventoryResolver.Resolve(data.Target);
            if (inventory == null) return null;

            // メインインベントリのときはホットバーを整理対象から除外する
            // Exclude the hotbar from sorting when the target is the main inventory.
            var excludeSlots = data.Target.InventoryType == InventoryType.Main
                ? PlayerInventoryConst.HotBarSlots
                : Array.Empty<int>();

            InventorySortService.Sort(inventory, excludeSlots);

            return null;
        }

        #region MessagePack

        [MessagePackObject]
        public class SortInventoryProtocolMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public InventoryIdentifierMessagePack Target { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public SortInventoryProtocolMessagePack() { }

            public SortInventoryProtocolMessagePack(InventoryIdentifierMessagePack target)
            {
                Tag = ProtocolTag;
                Target = target;
            }
        }

        #endregion
    }
}
