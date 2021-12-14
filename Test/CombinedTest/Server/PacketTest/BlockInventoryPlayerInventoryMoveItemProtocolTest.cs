using System.Collections.Generic;
using System.Linq;
using Core.Block;
using Core.Block.Config;
using Core.Block.Machine;
using Core.Block.Machine.util;
using Core.Block.RecipeConfig;
using Core.Config.Item;
using Core.Item;
using Core.Item.Config;
using Core.Item.Util;
using Game.PlayerInventory.Interface;
using Game.World.Interface;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using PlayerInventory;
using Server;
using Server.Util;
using World;

namespace Test.CombinedTest.Server.PacketTest
{
    public class BlockInventoryPlayerInventoryMoveItemProtocolTest
    {
        private ItemStackFactory _itemStackFactory = new ItemStackFactory(new TestItemConfig());
        [Test]
        public void ItemMoveTest()
        {
            int playerId = 1;
            int playerSlotIndex = 2;
            int blockInventorySlotIndex = 0;
            
            //初期設定----------------------------------------------------------
            
            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create();
            //ブロックの設置
            var blockDataStore = serviceProvider.GetService<IWorldBlockDatastore>();
            var block = NormalMachineFactory.Create(1,1,new NullIBlockInventory(),new TestBlockConfig(),new TestMachineRecipeConfig(_itemStackFactory),_itemStackFactory);
            blockDataStore.AddBlock(block, 0, 0, block);
            //ブロックにアイテムを挿入
            block.InsertItem(_itemStackFactory.Create(1,5));
            Assert.AreEqual(1,block.InputSlotWithoutEmptyItemStack[blockInventorySlotIndex].Id);
            Assert.AreEqual(5,block.InputSlotWithoutEmptyItemStack[blockInventorySlotIndex].Amount);
            
            //プレイヤーのインベントリの設定
            var payload = new List<byte>();
            payload.AddRange(ByteListConverter.ToByteArray((short)3));
            payload.AddRange(ByteListConverter.ToByteArray(playerId));
            packet.GetPacketResponse(payload);
            var playerInventoryData = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(playerId);
            
            
            //実際にアイテムを移動するテスト--------------------------------------------------------
            
            //ブロックインベントリからプレイヤーインベントリへアイテムを移す
            packet.GetPacketResponse(CreateReplacePayload(1,playerId,playerSlotIndex,0,0,blockInventorySlotIndex,5));
            //実際に移動できたか確認
            Assert.AreEqual(0,block.InputSlotWithoutEmptyItemStack.Count);
            Assert.AreEqual(1,playerInventoryData.GetItem(playerSlotIndex).Id);
            Assert.AreEqual(5,playerInventoryData.GetItem(playerSlotIndex).Amount);
            
            
            
            //プレイヤーインベントリからブロックインベントリへアイテムを移す
            packet.GetPacketResponse(CreateReplacePayload(0,playerId,playerSlotIndex,0,0,blockInventorySlotIndex,5));
            //きちんと移動できたか確認
            Assert.AreEqual(1,block.InputSlotWithoutEmptyItemStack[blockInventorySlotIndex].Id);
            Assert.AreEqual(5,block.InputSlotWithoutEmptyItemStack[blockInventorySlotIndex].Amount);
            Assert.AreEqual(ItemConst.NullItemId,playerInventoryData.GetItem(playerSlotIndex).Id);
            Assert.AreEqual(0,playerInventoryData.GetItem(playerSlotIndex).Amount);
            
            
            
            //別のアイテムIDが在ったとき、全て選択していれば入れ替える
            //別IDのアイテム挿入
            playerInventoryData.SetItem(playerSlotIndex, _itemStackFactory.Create(2,3));
            //プレイヤーインベントリからブロックインベントリへ全てのアイテムを移す
            packet.GetPacketResponse(CreateReplacePayload(0,playerId,playerSlotIndex,0,0,blockInventorySlotIndex,3));
            //きちんと移動できたか確認
            Assert.AreEqual(2,block.InputSlotWithoutEmptyItemStack[blockInventorySlotIndex].Id);
            Assert.AreEqual(3,block.InputSlotWithoutEmptyItemStack[blockInventorySlotIndex].Amount);
            Assert.AreEqual(1,playerInventoryData.GetItem(playerSlotIndex).Id);
            Assert.AreEqual(5,playerInventoryData.GetItem(playerSlotIndex).Amount);
            
            
            
            //ブロックから一部だけ移動させようとしても移動できないテスト
            packet.GetPacketResponse(CreateReplacePayload(0,playerId,playerSlotIndex,0,0,blockInventorySlotIndex,3));
            //移動できてないかの確認
            Assert.AreEqual(2,block.InputSlotWithoutEmptyItemStack[blockInventorySlotIndex].Id);
            Assert.AreEqual(3,block.InputSlotWithoutEmptyItemStack[blockInventorySlotIndex].Amount);
            Assert.AreEqual(1,playerInventoryData.GetItem(playerSlotIndex).Id);
            Assert.AreEqual(5,playerInventoryData.GetItem(playerSlotIndex).Amount);
            
            //一部だけ移動させようとしても移動できないテスト
            packet.GetPacketResponse(CreateReplacePayload(1,playerId,playerSlotIndex,0,0,blockInventorySlotIndex,2));
            //移動できてないかの確認
            Assert.AreEqual(2,block.InputSlotWithoutEmptyItemStack[blockInventorySlotIndex].Id);
            Assert.AreEqual(3,block.InputSlotWithoutEmptyItemStack[blockInventorySlotIndex].Amount);
            Assert.AreEqual(1,playerInventoryData.GetItem(playerSlotIndex).Id);
            Assert.AreEqual(5,playerInventoryData.GetItem(playerSlotIndex).Amount);
            
            //同じIDならそのまま足されるテスト
            //テスト用にブロックと同じアイテムIDを挿入
            playerInventoryData.SetItem(playerSlotIndex, _itemStackFactory.Create(2,3));
            //プレイヤーからアイテム2つを移す
            packet.GetPacketResponse(CreateReplacePayload(0,playerId,playerSlotIndex,0,0,blockInventorySlotIndex,2));
            
            Assert.AreEqual(2,block.InputSlotWithoutEmptyItemStack[blockInventorySlotIndex].Id);
            Assert.AreEqual(5,block.InputSlotWithoutEmptyItemStack[blockInventorySlotIndex].Amount);
            Assert.AreEqual(2,playerInventoryData.GetItem(playerSlotIndex).Id);
            Assert.AreEqual(1,playerInventoryData.GetItem(playerSlotIndex).Amount);
            
            //アイテムスタック数以上のアイテムを入れたときに戻されるテスト
            var max = new TestItemConfig().GetItemConfig(2).Stack;
            playerInventoryData.SetItem(playerSlotIndex, _itemStackFactory.Create(2,max));
            //プレイヤーからアイテムを全て移す
            packet.GetPacketResponse(CreateReplacePayload(0,playerId,playerSlotIndex,0,0,blockInventorySlotIndex,max));
            
            Assert.AreEqual(2,block.InputSlotWithoutEmptyItemStack[blockInventorySlotIndex].Id);
            Assert.AreEqual(max,block.InputSlotWithoutEmptyItemStack[blockInventorySlotIndex].Amount);
            Assert.AreEqual(2,playerInventoryData.GetItem(playerSlotIndex).Id);
            Assert.AreEqual(5,playerInventoryData.GetItem(playerSlotIndex).Amount);
            //逆の場合
            packet.GetPacketResponse(CreateReplacePayload(1,playerId,playerSlotIndex,0,0,blockInventorySlotIndex,max));
            
            Assert.AreEqual(2,block.InputSlotWithoutEmptyItemStack[blockInventorySlotIndex].Id);
            Assert.AreEqual(5,block.InputSlotWithoutEmptyItemStack[blockInventorySlotIndex].Amount);
            Assert.AreEqual(2,playerInventoryData.GetItem(playerSlotIndex).Id);
            Assert.AreEqual(max,playerInventoryData.GetItem(playerSlotIndex).Amount);
        }

        private List<byte> CreateReplacePayload(short blockToPlayerFlag,int playerId,int playerSlotIndex,int x,int y,int blockSlotIndex,int moveItemNum)
        {
            var payload = new List<byte>();
            payload.AddRange(ByteListConverter.ToByteArray((short)5));
            payload.AddRange(ByteListConverter.ToByteArray(blockToPlayerFlag)); //ブロック→プレイヤーのフラグ
            payload.AddRange(ByteListConverter.ToByteArray(playerId));
            payload.AddRange(ByteListConverter.ToByteArray(playerSlotIndex)); //プレイヤーインベントリの移動先スロット
            payload.AddRange(ByteListConverter.ToByteArray(x)); //ブロックX座標
            payload.AddRange(ByteListConverter.ToByteArray(y)); //ブロックY座標
            payload.AddRange(ByteListConverter.ToByteArray(blockSlotIndex)); //ブロックインベントリインデクス
            payload.AddRange(ByteListConverter.ToByteArray(moveItemNum)); //移動するアイテム数
            return payload;
        }
    }
}