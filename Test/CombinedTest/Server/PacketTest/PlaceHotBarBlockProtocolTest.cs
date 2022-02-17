using System.Collections.Generic;
using Core.Item;
using Core.Item.Util;
using Game.PlayerInventory.Interface;
using Game.World.Interface.DataStore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server;
using Server.Util;

namespace Test.CombinedTest.Server.PacketTest
{
    public class PlaceHotBarBlockProtocolTest
    {
        private const int PlacedBlockId = 1;
        private const int BlockItemId = 1;
        private const int PlayerId = 3;
        private const int HotBarSlot = 3;

        [Test]
        public void BlockPlaceTest()
        {
            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create();
            var itemStackFactory = serviceProvider.GetService<ItemStackFactory>();
            
            //パケットでプレイヤーインベントリを生成
            
            //ホットバーにアイテムとしてのブロックをセットする
            var slot = PlayerInventoryConst.HotBarSlotToInventorySlot(HotBarSlot);
            var inventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetMainInventoryData(PlayerId);
            inventory.SetItem(slot, itemStackFactory.Create(BlockItemId,3));
            
            //ブロックを置く
            packet.GetPacketResponse(CreateUseHotBarProtocol(2, 4));
            
            //ブロックが置かれているかチェック
            var world = serviceProvider.GetService<IWorldBlockDatastore>();
            Assert.AreEqual(PlacedBlockId, world.GetBlock(2, 4).GetBlockId());
            //アイテムが減っているかチェック
            Assert.AreEqual(2, inventory.GetItem(slot).Count);
            
            
            
            //既にブロックがあるところに置こうとしてもアイテムが減らないテスト
            packet.GetPacketResponse(CreateUseHotBarProtocol(2, 4));
            //アイテムが減っていないかのチェック
            Assert.AreEqual(2,
                inventory.GetItem(slot).Count);
            
            //ホットバー内のアイテムを使い切る
            packet.GetPacketResponse(CreateUseHotBarProtocol(3, 4));
            packet.GetPacketResponse(CreateUseHotBarProtocol(4, 4));
            //ホットバーのアイテムが空になっているかのテスト
            Assert.AreEqual(itemStackFactory.CreatEmpty(), inventory.GetItem(slot));
            
            
            //さらにブロックを置こうとしても置けないテスト
            packet.GetPacketResponse(CreateUseHotBarProtocol(10, 10));
            Assert.AreEqual(BlockConst.BlockConst.EmptyBlockId, world.GetBlock(10,10).GetBlockId());
        }

        private List<byte> CreateUseHotBarProtocol(int x,int y)
        {
            var payload = new List<byte>();
            payload.AddRange(ToByteList.Convert((short) 8));
            payload.AddRange(ToByteList.Convert((short) HotBarSlot));
            payload.AddRange(ToByteList.Convert(x));
            payload.AddRange(ToByteList.Convert(y));
            payload.AddRange(ToByteList.Convert(PlayerId));
            return payload;
        }


    }
}