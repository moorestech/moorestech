using System;
using Core.Master;
using Game.Block.Blocks.Chest;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.PlayerInventory.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol;
using Server.Protocol.PacketResponse.Util.InventoryMoveUtil;
using Server.Util.MessagePack;
using Tests.Module.TestMod;
using UnityEngine;
using static Server.Protocol.PacketResponse.SortInventoryProtocol;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class SortInventoryProtocolTest
    {
        private const int PlayerId = 0;

        [Test]
        public void MainInventorySortTest()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var mainInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).MainOpenableInventory;
            var itemStackFactory = ServerContext.ItemStackFactory;

            // バラけた・分割されたアイテムを上段スロットに配置する
            // Place scattered and split items into the upper (non-hotbar) slots.
            mainInventory.SetItem(0, new ItemId(2), 7);
            mainInventory.SetItem(2, new ItemId(3), 5);
            mainInventory.SetItem(5, new ItemId(1), 4);
            mainInventory.SetItem(8, new ItemId(1), 6);

            // ホットバー（最下段）にアイテムを置き、整理で動かないことを確認する
            // Put an item on the hotbar (bottom row) to verify it is left untouched.
            var hotBarSlot = PlayerInventoryConst.HotBarSlots[0];
            mainInventory.SetItem(hotBarSlot, new ItemId(5), 9);

            // メインインベントリを整理
            // Sort the main inventory.
            packet.GetPacketResponse(GetPacket(ItemMoveInventoryInfo.CreateMain()), new PacketResponseContext());

            // 同種が結合され、ItemId 昇順に詰め直されている
            // Same items are merged and re-packed in ItemId ascending order.
            Assert.AreEqual(itemStackFactory.Create(new ItemId(1), 10), mainInventory.GetItem(0));
            Assert.AreEqual(itemStackFactory.Create(new ItemId(2), 7), mainInventory.GetItem(1));
            Assert.AreEqual(itemStackFactory.Create(new ItemId(3), 5), mainInventory.GetItem(2));

            // 余ったスロットは空になっている
            // Remaining slots are emptied.
            Assert.AreEqual(ItemMaster.EmptyItemId, mainInventory.GetItem(3).Id);
            Assert.AreEqual(ItemMaster.EmptyItemId, mainInventory.GetItem(8).Id);

            // ホットバーは整理対象外なので不動
            // The hotbar is excluded from sorting and stays unchanged.
            Assert.AreEqual(itemStackFactory.Create(new ItemId(5), 9), mainInventory.GetItem(hotBarSlot));
        }

        [Test]
        public void MainInventoryStackOverflowMergeTest()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var mainInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).MainOpenableInventory;
            var itemStackFactory = ServerContext.ItemStackFactory;

            // 合計が最大スタックを超える同種アイテムを2スロットに分割配置する
            // Place a same item split across two slots so the total exceeds the max stack.
            var itemId = new ItemId(2);
            var maxStack = MasterHolder.ItemMaster.GetItemMaster(itemId).MaxStack;
            mainInventory.SetItem(0, itemId, maxStack - 5);
            mainInventory.SetItem(3, itemId, 10);

            packet.GetPacketResponse(GetPacket(ItemMoveInventoryInfo.CreateMain()), new PacketResponseContext());

            // 先頭スロットは最大スタックまで詰まり、あふれた5個が次スロットへ流れる
            // The first slot fills to max stack and the overflowing 5 items flow into the next slot.
            Assert.AreEqual(itemStackFactory.Create(itemId, maxStack), mainInventory.GetItem(0));
            Assert.AreEqual(itemStackFactory.Create(itemId, 5), mainInventory.GetItem(1));
            Assert.AreEqual(ItemMaster.EmptyItemId, mainInventory.GetItem(2).Id);
            Assert.AreEqual(ItemMaster.EmptyItemId, mainInventory.GetItem(3).Id);
        }

        [Test]
        public void BlockInventorySortTest()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var worldDataStore = ServerContext.WorldBlockDatastore;
            var itemStackFactory = ServerContext.ItemStackFactory;

            var chestPosition = new Vector3Int(5, 10);
            worldDataStore.TryAddBlock(ForUnitTestModBlockId.ChestId, chestPosition, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var chest);
            var chestComponent = chest.GetComponent<VanillaChestComponent>();

            // チェスト（itemSlotCount=5, slot0-4）へバラけた・分割されたアイテムを配置する
            // Place scattered and split items into the chest (itemSlotCount=5, slots 0-4).
            chestComponent.SetItem(1, new ItemId(2), 5);
            chestComponent.SetItem(2, new ItemId(1), 4);
            chestComponent.SetItem(4, new ItemId(1), 6);

            // チェスト（サブインベントリ）を整理
            // Sort the chest (sub-inventory).
            packet.GetPacketResponse(GetPacket(ItemMoveInventoryInfo.CreateSubInventory(InventoryIdentifierMessagePack.CreateBlockMessage(chestPosition))), new PacketResponseContext());

            // 同種結合＋ItemId 昇順（ホットバー除外なし、全スロット対象）
            // Same items merged and re-packed in ItemId order (no hotbar exclusion; all slots).
            Assert.AreEqual(itemStackFactory.Create(new ItemId(1), 10), chestComponent.GetItem(0));
            Assert.AreEqual(itemStackFactory.Create(new ItemId(2), 5), chestComponent.GetItem(1));
            Assert.AreEqual(ItemMaster.EmptyItemId, chestComponent.GetItem(2).Id);
            Assert.AreEqual(ItemMaster.EmptyItemId, chestComponent.GetItem(4).Id);
        }

        private byte[] GetPacket(ItemMoveInventoryInfo target)
        {
            return MessagePackSerializer.Serialize(new SortInventoryProtocolMessagePack(PlayerId, target));
        }
    }
}
