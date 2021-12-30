using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Core.Block;
using Core.Block.BlockFactory;
using Core.Block.Config;
using Core.Block.Machine;
using Core.Block.Machine.Inventory;
using Core.Block.Machine.InventoryController;
using Core.Block.RecipeConfig;
using Core.Item;
using Core.Item.Config;
using Core.Util;
using Game.PlayerInventory.Interface;
using Game.World.Interface;
using Game.World.Interface.DataStore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server;
using Server.Util;
using Test.Module;
using Test.Module.TestConfig;
using World;

namespace Test.CombinedTest.Server.PacketTest
{
    public class BlockInventoryPlayerInventoryMoveItemProtocolTest
    {
        private ItemStackFactory _itemStackFactory = new ItemStackFactory(new TestItemConfig());
        private BlockFactory _blockFactory;
        private VanillaMachine CreateMachine(int id,int indId)
        {
            if (_blockFactory == null)
            {
                _blockFactory = new BlockFactory(new AllMachineBlockConfig(),new VanillaIBlockTemplates(new TestMachineRecipeConfig(_itemStackFactory),_itemStackFactory));
            }
            var machine = _blockFactory.Create(id, indId) as VanillaMachine;
            return machine;
        }
        
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
            var block = CreateMachine(1,1);
            blockDataStore.AddBlock(block, 0, 0,BlockDirection.North);
            //ブロックにアイテムを挿入
            block.InsertItem(_itemStackFactory.Create(1,5));
            var input = GetInputSlot(block);
            Assert.AreEqual(1,input[blockInventorySlotIndex].Id);
            Assert.AreEqual(5,input[blockInventorySlotIndex].Count);
            
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
            input = GetInputSlot(block);
            Assert.AreEqual(0,input.Count);
            Assert.AreEqual(1,playerInventoryData.GetItem(playerSlotIndex).Id);
            Assert.AreEqual(5,playerInventoryData.GetItem(playerSlotIndex).Count);
            
            
            
            //プレイヤーインベントリからブロックインベントリへアイテムを移す
            packet.GetPacketResponse(CreateReplacePayload(0,playerId,playerSlotIndex,0,0,blockInventorySlotIndex,5));
            //きちんと移動できたか確認
            input = GetInputSlot(block);
            Assert.AreEqual(1,input[blockInventorySlotIndex].Id);
            Assert.AreEqual(5,input[blockInventorySlotIndex].Count);
            Assert.AreEqual(ItemConst.NullItemId,playerInventoryData.GetItem(playerSlotIndex).Id);
            Assert.AreEqual(0,playerInventoryData.GetItem(playerSlotIndex).Count);
            
            
            
            //別のアイテムIDが在ったとき、全て選択していれば入れ替える
            //別IDのアイテム挿入
            playerInventoryData.SetItem(playerSlotIndex, _itemStackFactory.Create(2,3));
            //プレイヤーインベントリからブロックインベントリへ全てのアイテムを移す
            packet.GetPacketResponse(CreateReplacePayload(0,playerId,playerSlotIndex,0,0,blockInventorySlotIndex,3));
            //きちんと移動できたか確認
            input = GetInputSlot(block);
            Assert.AreEqual(2,input[blockInventorySlotIndex].Id);
            Assert.AreEqual(3,input[blockInventorySlotIndex].Count);
            Assert.AreEqual(1,playerInventoryData.GetItem(playerSlotIndex).Id);
            Assert.AreEqual(5,playerInventoryData.GetItem(playerSlotIndex).Count);
            
            
            
            //ブロックから一部だけ移動させようとしても移動できないテスト
            packet.GetPacketResponse(CreateReplacePayload(0,playerId,playerSlotIndex,0,0,blockInventorySlotIndex,3));
            //移動できてないかの確認
            input = GetInputSlot(block);
            Assert.AreEqual(2,input[blockInventorySlotIndex].Id);
            Assert.AreEqual(3,input[blockInventorySlotIndex].Count);
            Assert.AreEqual(1,playerInventoryData.GetItem(playerSlotIndex).Id);
            Assert.AreEqual(5,playerInventoryData.GetItem(playerSlotIndex).Count);
            
            //一部だけ移動させようとしても移動できないテスト
            packet.GetPacketResponse(CreateReplacePayload(1,playerId,playerSlotIndex,0,0,blockInventorySlotIndex,2));
            //移動できてないかの確認
            input = GetInputSlot(block);
            Assert.AreEqual(2,input[blockInventorySlotIndex].Id);
            Assert.AreEqual(3,input[blockInventorySlotIndex].Count);
            Assert.AreEqual(1,playerInventoryData.GetItem(playerSlotIndex).Id);
            Assert.AreEqual(5,playerInventoryData.GetItem(playerSlotIndex).Count);
            
            //同じIDならそのまま足されるテスト
            //テスト用にブロックと同じアイテムIDを挿入
            playerInventoryData.SetItem(playerSlotIndex, _itemStackFactory.Create(2,3));
            //プレイヤーからアイテム2つを移す
            packet.GetPacketResponse(CreateReplacePayload(0,playerId,playerSlotIndex,0,0,blockInventorySlotIndex,2));
            
            input = GetInputSlot(block);
            Assert.AreEqual(2,input[blockInventorySlotIndex].Id);
            Assert.AreEqual(5,input[blockInventorySlotIndex].Count);
            Assert.AreEqual(2,playerInventoryData.GetItem(playerSlotIndex).Id);
            Assert.AreEqual(1,playerInventoryData.GetItem(playerSlotIndex).Count);
            
            //アイテムスタック数以上のアイテムを入れたときに戻されるテスト
            var max = new TestItemConfig().GetItemConfig(2).Stack;
            playerInventoryData.SetItem(playerSlotIndex, _itemStackFactory.Create(2,max));
            //プレイヤーからアイテムを全て移す
            packet.GetPacketResponse(CreateReplacePayload(0,playerId,playerSlotIndex,0,0,blockInventorySlotIndex,max));
            
            input = GetInputSlot(block);
            Assert.AreEqual(2,input[blockInventorySlotIndex].Id);
            Assert.AreEqual(max,input[blockInventorySlotIndex].Count);
            Assert.AreEqual(2,playerInventoryData.GetItem(playerSlotIndex).Id);
            Assert.AreEqual(5,playerInventoryData.GetItem(playerSlotIndex).Count);
            //逆の場合
            packet.GetPacketResponse(CreateReplacePayload(1,playerId,playerSlotIndex,0,0,blockInventorySlotIndex,max));
            
            input = GetInputSlot(block);
            Assert.AreEqual(2,input[blockInventorySlotIndex].Id);
            Assert.AreEqual(5,input[blockInventorySlotIndex].Count);
            Assert.AreEqual(2,playerInventoryData.GetItem(playerSlotIndex).Id);
            Assert.AreEqual(max,playerInventoryData.GetItem(playerSlotIndex).Count);
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
        
        
        public List<IItemStack> GetInputSlot(VanillaMachine machine)
        {
            var _vanillaMachineInventory = (VanillaMachineInventory)typeof(VanillaMachine).GetField("_vanillaMachineInventory",BindingFlags.NonPublic | BindingFlags.Instance).GetValue(machine);
            var _vanillaMachineInputInventory = (VanillaMachineInputInventory)typeof(VanillaMachineInventory)
                .GetField("_vanillaMachineInputInventory", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(_vanillaMachineInventory);

            return _vanillaMachineInputInventory.InputSlot.Where(i => i.Count != 0).ToList();
        }
    }
}