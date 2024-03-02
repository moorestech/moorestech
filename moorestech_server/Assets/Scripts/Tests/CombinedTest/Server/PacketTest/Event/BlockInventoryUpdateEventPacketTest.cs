using System.Collections.Generic;
using System.Linq;
using Core.Inventory;
using Core.Item;
using Game.Block.Interface;
using Game.World.Interface.DataStore;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Event.EventReceive;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Server.PacketTest.Event
{
    /// <summary>
    ///     ブロックのインベントリが更新された時、イベントのパケットが更新されているかをテストする
    /// </summary>
    public class BlockInventoryUpdateEventPacketTest
    {
        private const int MachineBlockId = 1;
        private const int PlayerId = 3;
        private const short PacketId = 16;

        //正しくインベントリの情報が更新されたことを通知するパケットが送られるかチェックする
        [Test]
        public void BlockInventoryUpdatePacketTest()
        {
            var (packetResponse, serviceProvider) = new MoorestechServerDiContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);

            var worldBlockDataStore = serviceProvider.GetService<IWorldBlockDatastore>();
            var blockFactory = serviceProvider.GetService<IBlockFactory>();
            var itemStackFactory = serviceProvider.GetService<ItemStackFactory>();

            //ブロックをセットアップ
            var block = blockFactory.Create(MachineBlockId, 1);
            var blockInventory = (IOpenableInventory)block;
            worldBlockDataStore.AddBlock(block, 5, 7, BlockDirection.North);


            //インベントリを開く
            packetResponse.GetPacketResponse(OpenCloseBlockInventoryPacket(5, 7, true));
            //ブロックにアイテムを入れる
            blockInventory.SetItem(1, itemStackFactory.Create(4, 8));


            //パケットが送られていることをチェック
            //イベントパケットを取得
            var eventPacket = packetResponse.GetPacketResponse(GetEventPacket());


            var eventMessagePack = MessagePackSerializer.Deserialize<ResponseEventProtocolMessagePack>(eventPacket[0].ToArray());
            //イベントパケットをチェック
            Assert.AreEqual(1, eventMessagePack.Events.Count);
            var payLoad = eventMessagePack.Events[0].Payload;
            var data = MessagePackSerializer.Deserialize<OpenableBlockInventoryUpdateEventMessagePack>(payLoad);

            Assert.AreEqual(1, data.Slot); // slot id
            Assert.AreEqual(4, data.Item.Id); // item id
            Assert.AreEqual(8, data.Item.Count); // item count
            Assert.AreEqual(5, data.X); // x
            Assert.AreEqual(7, data.Y); // y


            //ブロックのインベントリを閉じる
            packetResponse.GetPacketResponse(OpenCloseBlockInventoryPacket(5, 7, false));

            //ブロックにアイテムを入れる
            blockInventory.SetItem(2, itemStackFactory.Create(4, 8));


            //パケットが送られていないことをチェック
            //イベントパケットを取得
            eventPacket = packetResponse.GetPacketResponse(GetEventPacket());
            eventMessagePack = MessagePackSerializer.Deserialize<ResponseEventProtocolMessagePack>(eventPacket[0].ToArray());
            Assert.AreEqual(0, eventMessagePack.Events.Count);
        }


        //インベントリが開けるのは１つまでであることをテストする
        [Test]
        public void OnlyOneInventoryCanBeOpenedTest()
        {
            var (packetResponse, serviceProvider) = new MoorestechServerDiContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);

            var worldBlockDataStore = serviceProvider.GetService<IWorldBlockDatastore>();
            var blockFactory = serviceProvider.GetService<IBlockFactory>();
            var itemStackFactory = serviceProvider.GetService<ItemStackFactory>();

            //ブロック1をセットアップ
            var block1 = blockFactory.Create(MachineBlockId, 1);
            var block1Inventory = (IOpenableInventory)block1;
            worldBlockDataStore.AddBlock(block1, 5, 7, BlockDirection.North);
            //ブロック2をセットアップ
            var block2 = blockFactory.Create(MachineBlockId, 2);
            worldBlockDataStore.AddBlock(block2, 10, 20, BlockDirection.North);


            //一つ目のブロックインベントリを開く
            packetResponse.GetPacketResponse(OpenCloseBlockInventoryPacket(5, 7, true));
            //二つ目のブロックインベントリを開く
            packetResponse.GetPacketResponse(OpenCloseBlockInventoryPacket(10, 20, true));


            //一つ目のブロックインベントリにアイテムを入れる
            block1Inventory.SetItem(2, itemStackFactory.Create(4, 8));


            //パケットが送られていないことをチェック
            var response = packetResponse.GetPacketResponse(GetEventPacket());
            var eventMessagePack = MessagePackSerializer.Deserialize<ResponseEventProtocolMessagePack>(response[0].ToArray());
            Assert.AreEqual(0, eventMessagePack.Events.Count);
        }


        private List<byte> OpenCloseBlockInventoryPacket(int x, int y, bool isOpen)
        {
            return MessagePackSerializer
                .Serialize(new BlockInventoryOpenCloseProtocolMessagePack(PlayerId, x, y, isOpen)).ToList();
        }

        private List<byte> GetEventPacket()
        {
            return MessagePackSerializer.Serialize(new EventProtocolMessagePack(PlayerId)).ToList();
        }
    }
}