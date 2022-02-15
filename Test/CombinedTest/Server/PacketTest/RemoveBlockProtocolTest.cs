using System.Collections.Generic;
using Core.Block.BlockFactory;
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
            int playerSlotIndex = 2;

            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create();
            var worldBlock = serviceProvider.GetService<IWorldBlockDatastore>();
            var BlockFactory = serviceProvider.GetService<BlockFactory>();
            var Blockconfig = serviceProvider.GetService<IBlockConfig>();
            

            var payload = new List<byte>();
            payload.AddRange(ToByteList.Convert((short) 3));
            payload.AddRange(ToByteList.Convert(playerId));
            packet.GetPacketResponse(payload);
            var playerInventoryData =
                serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(playerId);

            var Block = BlockFactory.Create(1, 0);
          
            //削除するためのブロックの生成
            worldBlock.AddBlock(Block, 0, 0, BlockDirection.North);

            var BlockItemId = Blockconfig.GetBlockConfig(Block.GetBlockId());



            Assert.AreEqual(0,worldBlock.GetBlock(0,0).GetIntId());
            
            //プレイヤーインベントリに削除したブロックを追加

            playerInventoryData.InsertItem(_itemStackFactory.Create(BlockItemId.ItemId,1));

            //プロトコルを使ってブロックを削除
            packet.GetPacketResponse(RemoveBlock(0, 0, 0));

            Assert.False(worldBlock.Exists(0,0));
            
            
            //削除したブロックがプレイヤーインベントリに追加されているか
            Assert.AreEqual(BlockItemId.ItemId, playerInventoryData.GetItem(playerSlotIndex).Id);
            Assert.AreEqual(1, playerInventoryData.GetItem(playerSlotIndex).Count);

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