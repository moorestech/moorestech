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

        [Test]
        public void RemoveTest()
        {
            int playerId = 0;
            int playerSlotIndex = 0;

            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create();
            var worldBlock = serviceProvider.GetService<IWorldBlockDatastore>();
            var blockFactory = serviceProvider.GetService<BlockFactory>();
            var blockConfig = serviceProvider.GetService<IBlockConfig>();
            var itemStackFactory = serviceProvider.GetService<ItemStackFactory>();
            
            var playerInventoryData =
                serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(playerId);

            var block = blockFactory.Create(MachineBlockId, 0);
            var blockInventory = (IBlockInventory) block;
            blockInventory.InsertItem(itemStackFactory.Create(10, 7));
            var blockConfigData = blockConfig.GetBlockConfig(block.GetBlockId());
          
            //削除するためのブロックの生成
            worldBlock.AddBlock(block, 0, 0, BlockDirection.North);
            
            Assert.AreEqual(0,worldBlock.GetBlock(0,0).GetEntityId());
            
            //プレイヤーインベントリに削除したブロックを追加
            
            //プロトコルを使ってブロックを削除
            packet.GetPacketResponse(RemoveBlock(0, 0, 0));

            Assert.False(worldBlock.Exists(0,0));
            
            
            //削除したブロックがプレイヤーインベントリに追加されているか
            Assert.AreEqual(blockConfigData.ItemId, playerInventoryData.MainInventory.GetItem(playerSlotIndex).Id);
            Assert.AreEqual(1, playerInventoryData.MainInventory.GetItem(playerSlotIndex).Count);
            
            //ブロック内のアイテムがインベントリに入っているか
            Assert.AreEqual(10, playerInventoryData.MainInventory.GetItem(playerSlotIndex+1).Id);
            Assert.AreEqual(7, playerInventoryData.MainInventory.GetItem(playerSlotIndex+1).Count);

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