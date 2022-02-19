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
        
        private ItemStackFactory _itemStackFactory = new ItemStackFactory(new TestItemConfig());

        [Test]
        public void RemoveTest()
        {
            int playerId = 0;
            int playerSlotIndex = 0;
            int MachineBlockId = 1;

            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create();
            var worldBlock = serviceProvider.GetService<IWorldBlockDatastore>();
            var BlockFactory = serviceProvider.GetService<BlockFactory>();
            var Blockconfig = serviceProvider.GetService<IBlockConfig>();
            
            
            var playerInventoryData =
                serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(playerId);

            var Block = BlockFactory.Create(MachineBlockId, 0);
            var BlockInventory = (IBlockInventory) Block;
            BlockInventory.InsertItem(_itemStackFactory.Create(10, 7));
            var blockConfigData = Blockconfig.GetBlockConfig(Block.GetBlockId());
          
            //削除するためのブロックの生成
            worldBlock.AddBlock(Block, 0, 0, BlockDirection.North);
            
            Assert.AreEqual(0,worldBlock.GetBlock(0,0).GetIntId());
            
            //プレイヤーインベントリに削除したブロックを追加
            
            //プロトコルを使ってブロックを削除
            packet.GetPacketResponse(RemoveBlock(0, 0, 0));

            Assert.False(worldBlock.Exists(0,0));
            
            
            //削除したブロックがプレイヤーインベントリに追加されているか
            Assert.AreEqual(blockConfigData.ItemId, playerInventoryData.MainInventory.GetItem(playerSlotIndex).Id);
            Assert.AreEqual(1, playerInventoryData.MainInventory.GetItem(playerSlotIndex).Count);

        }

        List<byte> RemoveBlock(int x, int y,int Playerid)
        {
            var bytes = new List<byte>();
            bytes.AddRange(ToByteList.Convert((short) 10));
            bytes.AddRange(ToByteList.Convert(x));
            bytes.AddRange(ToByteList.Convert(y));
            bytes.AddRange(ToByteList.Convert(Playerid));

            return bytes;
        }
        
    }
}