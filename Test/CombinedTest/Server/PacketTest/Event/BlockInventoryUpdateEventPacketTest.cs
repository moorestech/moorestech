using System;
using System.Collections.Generic;
using System.Linq;
using Core.Block.BlockFactory;
using Core.Inventory;
using Core.Item;
using Game.World.Interface.DataStore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server;
using Server.Util;

namespace Test.CombinedTest.Server.PacketTest.Event
{
    /// <summary>
    /// ブロックのインベントリが更新された時、イベントのパケットが更新されているかをテストする
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
            var (packetResponse, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create();
            
            var worldBlockDataStore = serviceProvider.GetService<IWorldBlockDatastore>();
            var blockFactory = serviceProvider.GetService<BlockFactory>();
            var itemStackFactory = serviceProvider.GetService<ItemStackFactory>();
            
            //ブロックをセットアップ
            var block = blockFactory.Create(MachineBlockId, 1);
            var blockInventory = (IOpenableInventory)block;
            worldBlockDataStore.AddBlock(block, 5, 7, BlockDirection.North);
            
            
            //インベントリを開く
            packetResponse.GetPacketResponse(OpenCloseBlockInventoryPacket(5,7,1));
            //ブロックにアイテムを入れる
            blockInventory.SetItem(1,itemStackFactory.Create(4,8));
            
            
            //パケットが送られていることをチェック
            //イベントパケットを取得
            var eventPacket = packetResponse.GetPacketResponse(GetEventPacket());
            
            //イベントパケットをチェック
            Assert.AreEqual(1,eventPacket.Count);
            var packetEnumerator = new ByteListEnumerator(eventPacket[0].ToList());
            Assert.AreEqual(3,packetEnumerator.MoveNextToGetShort());
            Assert.AreEqual(2,packetEnumerator.MoveNextToGetShort());
            Assert.AreEqual(1,packetEnumerator.MoveNextToGetInt()); // slot id
            Assert.AreEqual(4,packetEnumerator.MoveNextToGetInt()); // item id
            Assert.AreEqual(8,packetEnumerator.MoveNextToGetInt()); // item count
            Assert.AreEqual(5,packetEnumerator.MoveNextToGetInt()); // x
            Assert.AreEqual(7,packetEnumerator.MoveNextToGetInt()); // y
            
            
            //ブロックのインベントリを閉じる
            packetResponse.GetPacketResponse(OpenCloseBlockInventoryPacket(5,7,0));
            
            //ブロックにアイテムを入れる
            blockInventory.SetItem(2,itemStackFactory.Create(4,8));
            
            
            //パケットが送られていないことをチェック
            //イベントパケットを取得
            eventPacket = packetResponse.GetPacketResponse(GetEventPacket());
            //イベントパケットをチェック
            Assert.AreEqual(0,eventPacket.Count);
        }
        
        
        //インベントリが開けるのは１つまでであることをテストする
        [Test]
        public void OnlyOneInventoryCanBeOpenedTest()
        {
            var (packetResponse, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create();
            
            var worldBlockDataStore = serviceProvider.GetService<IWorldBlockDatastore>();
            var blockFactory = serviceProvider.GetService<BlockFactory>();
            var itemStackFactory = serviceProvider.GetService<ItemStackFactory>();
            
            //ブロック1をセットアップ
            var block1 = blockFactory.Create(MachineBlockId, 1);
            var block1Inventory = (IOpenableInventory)block1;
            worldBlockDataStore.AddBlock(block1, 5, 7, BlockDirection.North);
            //ブロック2をセットアップ
            var block2 = blockFactory.Create(MachineBlockId, 2);
            worldBlockDataStore.AddBlock(block2, 10, 20, BlockDirection.North);
            
            
            
            //一つ目のブロックインベントリを開く
            packetResponse.GetPacketResponse(OpenCloseBlockInventoryPacket(5,7,0));
            //二つ目のブロックインベントリを開く
            packetResponse.GetPacketResponse(OpenCloseBlockInventoryPacket(10,20,0));
            
            
            //一つ目のブロックインベントリにアイテムを入れる
            block1Inventory.SetItem(2,itemStackFactory.Create(4,8));
            
            
            //パケットが送られていないことをチェック
            //イベントパケットを取得
            var eventPacket = packetResponse.GetPacketResponse(GetEventPacket());
            //イベントパケットをチェック
            Assert.AreEqual(0,eventPacket.Count);

        }
        
        
        /// <param name="openOrClose">1なら開く 0なら閉じる</param>
        private List<byte> OpenCloseBlockInventoryPacket(int x,int y,byte openOrClose)
        {
            var packet = new List<byte>();
            packet.AddRange(ToByteList.Convert(PacketId));
            packet.AddRange(ToByteList.Convert(x));
            packet.AddRange(ToByteList.Convert(y));
            packet.AddRange(ToByteList.Convert(PlayerId));
            packet.Add(openOrClose);
            return packet;
        }
        
        private List<byte> GetEventPacket()
        {
            var payload = new List<byte>();
            payload.AddRange(ToByteList.Convert((short) 4));
            payload.AddRange(ToByteList.Convert(PlayerId));
            return payload;
        }
        
    }
}