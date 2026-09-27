using System;
using System.Linq;
using Core.Item;
using Core.Master;
using Game.Block.Blocks.Chest;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.PlayerInventory.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol;
using Server.Util.MessagePack;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;
using static Server.Protocol.PacketResponse.SortInventoryProtocol;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class SortInventoryProtocolTest : SortInventoryProtocolTestBase
    {

        [Test]
        public void MainInventorySortTest()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var mainInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).MainOpenableInventory;
            var itemStackFactory = ServerContext.ItemStackFactory;

            // 分割アイテムを末尾込みで配置
            // Place scattered and split items, including the trailing slots.
            mainInventory.SetItem(0, new ItemId(2), 7);
            mainInventory.SetItem(2, new ItemId(3), 5);
            mainInventory.SetItem(5, new ItemId(1), 4);
            mainInventory.SetItem(8, new ItemId(1), 6);
            mainInventory.SetItem(mainInventory.GetSlotSize() - 1, new ItemId(5), 9);

            // メインインベントリを整理
            // Sort the main inventory.
            packet.GetPacketResponse(GetPacket(InventoryIdentifierMessagePack.CreateMainMessage()), Tests.Util.BoundPacketContext.Bind(PlayerId));

            // 同種結合しId昇順に再配置
            // Same items are merged and re-packed in ItemId ascending order (trailing slots included too).
            Assert.AreEqual(itemStackFactory.Create(new ItemId(1), 10), mainInventory.GetItem(0));
            Assert.AreEqual(itemStackFactory.Create(new ItemId(2), 7), mainInventory.GetItem(1));
            Assert.AreEqual(itemStackFactory.Create(new ItemId(3), 5), mainInventory.GetItem(2));
            Assert.AreEqual(itemStackFactory.Create(new ItemId(5), 9), mainInventory.GetItem(3));

            // 余ったスロットは空になっている
            // Remaining slots are emptied.
            Assert.AreEqual(ItemMaster.EmptyItemId, mainInventory.GetItem(4).Id);
            Assert.AreEqual(ItemMaster.EmptyItemId, mainInventory.GetItem(mainInventory.GetSlotSize() - 1).Id);
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
            var maxStack = ItemStackLevelDataStore.Instance.GetMaxStack(itemId);
            mainInventory.SetItem(0, itemId, maxStack - 5);
            mainInventory.SetItem(3, itemId, 10);

            packet.GetPacketResponse(GetPacket(InventoryIdentifierMessagePack.CreateMainMessage()), Tests.Util.BoundPacketContext.Bind(PlayerId));

            // 先頭スロットは最大スタックまで詰まり、あふれた5個が次スロットへ流れる
            // The first slot fills to max stack and the overflowing 5 items flow into the next slot.
            Assert.AreEqual(itemStackFactory.Create(itemId, maxStack), mainInventory.GetItem(0));
            Assert.AreEqual(itemStackFactory.Create(itemId, 5), mainInventory.GetItem(1));
            Assert.AreEqual(ItemMaster.EmptyItemId, mainInventory.GetItem(2).Id);
            Assert.AreEqual(ItemMaster.EmptyItemId, mainInventory.GetItem(3).Id);
        }
    }
}
