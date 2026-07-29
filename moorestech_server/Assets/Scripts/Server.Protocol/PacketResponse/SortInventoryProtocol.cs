using System;
using System.Collections.Generic;
using System.Linq;
using Game.Block.Interface.Component;
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

            // インベントリ種別ごとに整理対象外のスロットを決める
            // Decide the slots excluded from sorting per inventory type.
            IEnumerable<int> excludeSlots = data.Target.InventoryType switch
            {
                // メインインベントリはホットバーを整理対象から外す
                // The main inventory keeps its hotbar out of sorting.
                InventoryType.Main => PlayerInventoryConst.GetHotBarSlots(inventory.GetSlotSize()),
                // 装備はスロット位置自体が意味を持つため全スロットを外す
                // Equipment slot positions carry meaning, so every slot is excluded.
                InventoryType.Equipment => Enumerable.Range(0, inventory.GetSlotSize()),
                _ => Array.Empty<int>(),
            };

            // インベントリ自身が除外スロットを宣言している場合（機械のモジュールスロット等）は結合する
            // Union slots declared by the inventory itself (e.g. machine module slots).
            if (inventory is ISortExcludedSlots sortExcluded)
            {
                excludeSlots = excludeSlots.Union(sortExcluded.SortExcludedSlots);
            }

            InventorySortService.Sort(inventory, excludeSlots.ToList());

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
