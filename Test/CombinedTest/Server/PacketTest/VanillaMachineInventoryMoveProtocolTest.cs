using System.Collections.Generic;
using Core.Block.BlockFactory;
using Core.Block.Blocks.Machine;
using Core.Item;
using Game.PlayerInventory.Interface;
using Game.World.Interface.DataStore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server;
using Server.Util;

namespace Test.CombinedTest.Server.PacketTest
{
    public class VanillaMachineInventoryMoveProtocolTest
    {
        private const int MachineBlockId = 1;
        [Test]
        public void BlockInventoryItemMove()
        {
            int playerId = 1;
            int playerSlotIndex = 2;
            int blockInventorySlotIndex = 0;

            //初期設定----------------------------------------------------------

            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create();
            var itemStackFactory = serviceProvider.GetService<ItemStackFactory>();

            //ブロックの作成、設置
            int x = 0;
            int y = 0;
            var blockInventoryData =
                (VanillaMachine)serviceProvider.GetService<BlockFactory>().Create(MachineBlockId,1);
            serviceProvider.GetService<IWorldBlockDatastore>().AddBlock(blockInventoryData,0,0,BlockDirection.East);

            //アイテムの設定
            blockInventoryData.SetItem(0, itemStackFactory.Create(1, 5));
            blockInventoryData.SetItem(1, itemStackFactory.Create(1, 1));
            blockInventoryData.SetItem(2, itemStackFactory.Create(2, 1));

            //実際に移動させてテスト
            //全てのアイテムを移動させるテスト
            packet.GetPacketResponse(BlockInventoryItemMove(0, 3, 5, x,y));
            Assert.AreEqual(blockInventoryData.GetItem(0), itemStackFactory.CreatEmpty());
            Assert.AreEqual(blockInventoryData.GetItem(3), itemStackFactory.Create(1, 5));

            //一部のアイテムを移動させるテスト
            packet.GetPacketResponse(BlockInventoryItemMove(3, 0, 3, x,y));
            Assert.AreEqual(blockInventoryData.GetItem(0), itemStackFactory.Create(1, 3));
            Assert.AreEqual(blockInventoryData.GetItem(3), itemStackFactory.Create(1, 2));

            //一部のアイテムを移動しようとするが他にスロットがあるため失敗するテスト
            packet.GetPacketResponse(BlockInventoryItemMove(0, 2, 1, x,y));
            Assert.AreEqual(blockInventoryData.GetItem(0), itemStackFactory.Create(1, 3));
            Assert.AreEqual(blockInventoryData.GetItem(2), itemStackFactory.Create(2, 1));

            //全てのアイテムを移動させるテスト
            packet.GetPacketResponse(BlockInventoryItemMove(0, 2, 3, x,y));
            Assert.AreEqual(blockInventoryData.GetItem(0), itemStackFactory.Create(2, 1));
            Assert.AreEqual(blockInventoryData.GetItem(2), itemStackFactory.Create(1, 3));

            //アイテムを加算するテスト
            packet.GetPacketResponse(BlockInventoryItemMove(2, 1, 3, x,y));
            Assert.AreEqual(blockInventoryData.GetItem(2), itemStackFactory.CreatEmpty());
            Assert.AreEqual(blockInventoryData.GetItem(1), itemStackFactory.Create(1, 4));
        }

        private List<byte> BlockInventoryItemMove(int fromSlot,int toSlot,int itemCount,int x,int y)
        {
            var payload = new List<byte>();
            payload.AddRange(ToByteList.Convert((short) 8));
            payload.AddRange(ToByteList.Convert(x));
            payload.AddRange(ToByteList.Convert(y));
            payload.AddRange(ToByteList.Convert(fromSlot));
            payload.AddRange(ToByteList.Convert(toSlot));
            payload.AddRange(ToByteList.Convert(itemCount));
            return payload;
        }
    }
}