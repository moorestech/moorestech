using System.Collections.Generic;
using System.Linq;
using Game.Block.Interface;
using Game.Block.Blocks.Chest;
using Game.Context;
using Game.PlayerInventory.Interface;
using Game.World.Interface.DataStore;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse;
using Server.Protocol.PacketResponse.Util.InventoryMoveUtil;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class InventoryItemMoveProtocolTest
    {
        private const int PlayerId = 0;
        private const int ChestBlockId = 7;

        [Test]
        public void MainInventoryMoveTest()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);

            var mainInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(0).MainOpenableInventory;
            var grabInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(0).GrabInventory;
            var itemStackFactory = ServerContext.ItemStackFactory;

            //インベントリの設定
            mainInventory.SetItem(0, 1, 10);

            //インベントリを持っているアイテムに移す
            packet.GetPacketResponse(GetPacket(7,
                new ItemMoveInventoryInfo(ItemMoveInventoryType.MainInventory), 0,
                new ItemMoveInventoryInfo(ItemMoveInventoryType.GrabInventory), 0));

            //移っているかチェック
            Assert.AreEqual(itemStackFactory.Create(1, 3), mainInventory.GetItem(0));
            Assert.AreEqual(itemStackFactory.Create(1, 7), grabInventory.GetItem(0));


            //持っているアイテムをインベントリに移す
            packet.GetPacketResponse(GetPacket(5,
                new ItemMoveInventoryInfo(ItemMoveInventoryType.GrabInventory), 0,
                new ItemMoveInventoryInfo(ItemMoveInventoryType.MainInventory), 0));


            //移っているかチェック
            Assert.AreEqual(itemStackFactory.Create(1, 8), mainInventory.GetItem(0));
            Assert.AreEqual(itemStackFactory.Create(1, 2), grabInventory.GetItem(0));
        }



        [Test]
        public void BlockInventoryTest()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);

            var grabInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(0).GrabInventory;
            var worldDataStore = ServerContext.WorldBlockDatastore;
            var itemStackFactory = ServerContext.ItemStackFactory;

            Vector3Int chestPosition = new Vector3Int(5, 10);

            var chestPosInfo = new BlockPositionInfo(chestPosition, BlockDirection.North, Vector3Int.one);
            var chest = ServerContext.BlockFactory.Create(ChestBlockId, 1, chestPosInfo);
            var chestComponent = chest.ComponentManager.GetComponent<VanillaChestComponent>();
            worldDataStore.AddBlock(chest);

            //ブロックインベントリの設定
            chestComponent.SetItem(1, 1, 10);

            //インベントリを持っているアイテムに移す
            packet.GetPacketResponse(GetPacket(7,
                new ItemMoveInventoryInfo(ItemMoveInventoryType.BlockInventory, new Vector3Int(5, 10)), 1 + PlayerInventoryConst.MainInventorySize,
                new ItemMoveInventoryInfo(ItemMoveInventoryType.GrabInventory), 0));

            //移っているかチェック
            Assert.AreEqual(itemStackFactory.Create(1, 3), chestComponent.GetItem(1));
            Assert.AreEqual(itemStackFactory.Create(1, 7), grabInventory.GetItem(0));


            //持っているアイテムをインベントリに移す
            packet.GetPacketResponse(GetPacket(5,
                new ItemMoveInventoryInfo(ItemMoveInventoryType.GrabInventory), 0,
                new ItemMoveInventoryInfo(ItemMoveInventoryType.BlockInventory, new Vector3Int(5, 10)), 1 + PlayerInventoryConst.MainInventorySize));

            //移っているかチェック
            Assert.AreEqual(itemStackFactory.Create(1, 8), chestComponent.GetItem(1));
            Assert.AreEqual(itemStackFactory.Create(1, 2), grabInventory.GetItem(0));
        }


        private List<byte> GetPacket(int count, ItemMoveInventoryInfo from, int fromSlot, ItemMoveInventoryInfo to, int toSlot,
            ItemMoveType itemMoveType = ItemMoveType.SwapSlot)
        {
            return MessagePackSerializer.Serialize(
                new InventoryItemMoveProtocolMessagePack(PlayerId, count, itemMoveType, from, fromSlot, to, toSlot)).ToList();
        }
    }
}