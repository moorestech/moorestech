using System.Collections.Generic;
using Core.Block.BlockFactory;
using Core.Block.BlockInventory;
using Core.Block.Config;
using Core.Block.Config.LoadConfig;
using Core.Item;
using Core.Item.Config;
using Game.PlayerInventory.Interface;
using Game.World.Interface.DataStore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server;
using Server.Event.EventReceive;
using Server.Util;

namespace Test.CombinedTest.Server.PacketTest
{
    public class RemoveBlockProtocolTest
    {
        private const int MachineBlockId = 1;
        private const int PlayerId = 0;

        [Test]
        public void RemoveTest()
        {
            int playerSlotIndex = 0;

            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create();
            var worldBlock = serviceProvider.GetService<IWorldBlockDatastore>();
            var blockFactory = serviceProvider.GetService<BlockFactory>();
            var blockConfig = serviceProvider.GetService<IBlockConfig>();
            var itemStackFactory = serviceProvider.GetService<ItemStackFactory>();
            
            var playerInventoryData =
                serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId);

            var block = blockFactory.Create(MachineBlockId, 0);
            var blockInventory = (IBlockInventory) block;
            blockInventory.InsertItem(itemStackFactory.Create(10, 7));
            var blockConfigData = blockConfig.GetBlockConfig(block.GetBlockId());
          
            //削除するためのブロックの生成
            worldBlock.AddBlock(block, 0, 0, BlockDirection.North);
            
            Assert.AreEqual(0,worldBlock.GetBlock(0,0).GetEntityId());
            
            //プレイヤーインベントリに削除したブロックを追加
            
            
            //プロトコルを使ってブロックを削除
            packet.GetPacketResponse(RemoveBlock(0, 0, PlayerId));

            
            //削除したブロックがワールドに存在しないことを確認
            Assert.False(worldBlock.Exists(0,0));
            
            
            //削除したブロックがプレイヤーインベントリに追加されているか
            Assert.AreEqual(blockConfigData.ItemId, playerInventoryData.MainInventory.GetItem(playerSlotIndex).Id);
            Assert.AreEqual(1, playerInventoryData.MainInventory.GetItem(playerSlotIndex).Count);
            
            //ブロック内のアイテムがインベントリに入っているか
            Assert.AreEqual(10, playerInventoryData.MainInventory.GetItem(playerSlotIndex+1).Id);
            Assert.AreEqual(7, playerInventoryData.MainInventory.GetItem(playerSlotIndex+1).Count);

        }

        
        //インベントリがいっぱいで一部のアイテムが残っている場合のテスト
        [Test]
        public void InventoryFullToRemoveBlockSomeItemRemainTest()
        {
            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create();
            var worldBlock = serviceProvider.GetService<IWorldBlockDatastore>();
            var blockFactory = serviceProvider.GetService<BlockFactory>();
            var itemConfig = serviceProvider.GetService<IItemConfig>();
            var itemStackFactory = serviceProvider.GetService<ItemStackFactory>();
            
            var mainInventory =
                serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).MainInventory;

            //インベントリの2つのスロットを残してインベントリを満杯にする
            for (int i = 2; i < mainInventory.GetSlotSize(); i++)
            {
                mainInventory.SetItem(i,itemStackFactory.Create(1000,1));
            }
            
            //一つの目のスロットにはID3の最大スタック数から1個少ないアイテムを入れる
            var id3MaxStack = itemConfig.GetItemConfig(3).MaxStack;
            mainInventory.SetItem(0,itemStackFactory.Create(3,id3MaxStack-1));
            //二つめのスロットには何も入れない
            
            
            
            
            //削除するためのブロックの生成
            var block = blockFactory.Create(MachineBlockId, 0);
            var blockInventory = (IBlockInventory) block;
            //ブロックにはID3のアイテムを2個と、ID4のアイテムを5個入れる
            //このブロックを削除したときに、ID3のアイテムが1個だけ残る
            blockInventory.SetItem(0,itemStackFactory.Create(3, 2));
            blockInventory.SetItem(1,itemStackFactory.Create(4, 5));
            
            //ブロックを設置
            worldBlock.AddBlock(block, 0, 0, BlockDirection.North);
            

            
            
            
            //プロトコルを使ってブロックを削除
            packet.GetPacketResponse(RemoveBlock(0, 0, PlayerId));

            
            
            
            //削除したブロックがワールドに存在してることを確認
            Assert.True(worldBlock.Exists(0,0));
            
            //プレイヤーのインベントリにアイテムが入っているか確認
            Assert.AreEqual(itemStackFactory.Create(3,id3MaxStack),mainInventory.GetItem(0));
            Assert.AreEqual(itemStackFactory.Create(4,5),mainInventory.GetItem(1));
            
            //ブロックのインベントリが減っているかを確認
            Assert.AreEqual(itemStackFactory.Create(3,1),blockInventory.GetItem(0));
            Assert.AreEqual(itemStackFactory.CreatEmpty(),blockInventory.GetItem(1));
            
        }
        
        //ブロックの中にアイテムはないけど、プレイヤーのインベントリが満杯でブロックを破壊できない時のテスト
        [Test]
        public void InventoryFullToCantRemoveBlockTest()
        {
            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create();
            var worldBlock = serviceProvider.GetService<IWorldBlockDatastore>();
            var blockFactory = serviceProvider.GetService<BlockFactory>();
            var itemConfig = serviceProvider.GetService<IItemConfig>();
            var itemStackFactory = serviceProvider.GetService<ItemStackFactory>();
            
            var mainInventory =
                serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).MainInventory;

            //インベントリを満杯にする
            for (int i = 0; i < mainInventory.GetSlotSize(); i++)
            {
                mainInventory.SetItem(i,itemStackFactory.Create(1000,1));
            }
            
            //ブロックを設置
            var block = blockFactory.Create(MachineBlockId, 0);
            worldBlock.AddBlock(block, 0, 0, BlockDirection.North);
            
            
            
            //プロトコルを使ってブロックを削除
            packet.GetPacketResponse(RemoveBlock(0, 0, PlayerId));
            
            
            
            //ブロックが削除できていないことを検証
            Assert.True(worldBlock.Exists(0,0));
        }
        
        
        List<byte> RemoveBlock(int x, int y,int playerId)
        {
            var bytes = new List<byte>();
            bytes.AddRange(ToByteList.Convert((short) 10));
            bytes.AddRange(ToByteList.Convert(x));
            bytes.AddRange(ToByteList.Convert(y));
            bytes.AddRange(ToByteList.Convert(playerId));

            return bytes;
        }
        
    }
}