using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.PlayerInventory.Interface;
using Game.World.Interface.DataStore;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;
using static Server.Protocol.PacketResponse.RemoveBlockProtocol;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class RemoveBlockProtocolTest
    {
        private const int MachineBlockId = 1;
        private const int PlayerId = 0;
        
        [Test]
        public void RemoveTest()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory, true);
            var worldBlock = ServerContext.WorldBlockDatastore;
            var itemStackFactory = ServerContext.ItemStackFactory;
            
            var playerInventoryData = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId);
            
            //削除するためのブロックの生成
            worldBlock.TryAddBlock(ForUnitTestModBlockId.MachineId, new Vector3Int(0, 0), BlockDirection.North, out var block);
            var blockInventory = block.GetComponent<IBlockInventory>();
            blockInventory.InsertItem(itemStackFactory.Create(new ItemId(10), 7));
            var blockElement = MasterHolder.BlockMaster.GetBlockMaster(block.BlockId);
            
            //プロトコルを使ってブロックを削除
            packet.GetPacketResponse(RemoveBlock(new Vector3Int(0, 0), PlayerId));
            
            
            //削除したブロックがワールドに存在しないことを確認
            Assert.False(worldBlock.Exists(new Vector3Int(0, 0)));
            
            
            var playerSlotIndex = PlayerInventoryConst.HotBarSlotToInventorySlot(0);
            //ブロック内のアイテムがインベントリに入っているか
            Assert.AreEqual(10, playerInventoryData.MainOpenableInventory.GetItem(playerSlotIndex).Id.AsPrimitive());
            Assert.AreEqual(7, playerInventoryData.MainOpenableInventory.GetItem(playerSlotIndex).Count);
            
            //削除したブロックは次のスロットに入っているのでそれをチェック
            var blockItemId = MasterHolder.ItemMaster.GetItemId(blockElement.ItemGuid);
            Assert.AreEqual(blockItemId, playerInventoryData.MainOpenableInventory.GetItem(playerSlotIndex + 1).Id);
            Assert.AreEqual(1, playerInventoryData.MainOpenableInventory.GetItem(playerSlotIndex + 1).Count);
        }
        
        
        //インベントリがいっぱいで一部のアイテムが残っている場合のテスト
        [Test]
        public void InventoryFullToRemoveBlockSomeItemRemainTest()
        {
            var (packet, serviceProvider) =
                new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory, true);
            var worldBlock = ServerContext.WorldBlockDatastore;
            var blockFactory = ServerContext.BlockFactory;
            var itemStackFactory = ServerContext.ItemStackFactory;
            
            var mainInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).MainOpenableInventory;
            
            //インベントリの2つのスロットを残してインベントリを満杯にする
            for (var i = 2; i < mainInventory.GetSlotSize(); i++)
                mainInventory.SetItem(i, itemStackFactory.Create(new ItemId(10), 1));
            
            //一つの目のスロットにはID3の最大スタック数から1個少ないアイテムを入れる
            var id3MaxStack = MasterHolder.ItemMaster.GetItemMaster(new ItemId(3)).MaxStack;
            mainInventory.SetItem(0, itemStackFactory.Create(new ItemId(3), id3MaxStack - 1));
            //二つめのスロットにはID4のアイテムを1つ入れておく
            mainInventory.SetItem(1, itemStackFactory.Create(new ItemId(4), 1));
            
            
            //削除するためのブロックを設置
            worldBlock.TryAddBlock(ForUnitTestModBlockId.MachineId, new Vector3Int(0, 0), BlockDirection.North, out var block);
            var blockInventory = block.GetComponent<IBlockInventory>();
            //ブロックにはID3のアイテムを2個と、ID4のアイテムを5個入れる
            //このブロックを削除したときに、ID3のアイテムが1個だけ残る
            blockInventory.SetItem(0, itemStackFactory.Create(new ItemId(3), 2));
            blockInventory.SetItem(1, itemStackFactory.Create(new ItemId(4), 5));
            
            
            //プロトコルを使ってブロックを削除
            packet.GetPacketResponse(RemoveBlock(new Vector3Int(0, 0), PlayerId));
            
            
            //削除したブロックがワールドに存在してることを確認
            Assert.True(worldBlock.Exists(new Vector3Int(0, 0)));
            
            //プレイヤーのインベントリにブロック内のアイテムが入っているか確認
            Assert.AreEqual(itemStackFactory.Create(new ItemId(3), id3MaxStack), mainInventory.GetItem(0));
            Assert.AreEqual(itemStackFactory.Create(new ItemId(4), 6), mainInventory.GetItem(1));
            
            //ブロックのインベントリが減っているかを確認
            Assert.AreEqual(itemStackFactory.Create(new ItemId(3), 1), blockInventory.GetItem(0));
            Assert.AreEqual(itemStackFactory.CreatEmpty(), blockInventory.GetItem(1));
        }
        
        //ブロックの中にアイテムはないけど、プレイヤーのインベントリが満杯でブロックを破壊できない時のテスト
        [Test]
        public void InventoryFullToCantRemoveBlockTest()
        {
            var (packet, serviceProvider) =
                new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory, true);
            var worldBlock = ServerContext.WorldBlockDatastore;
            var itemStackFactory = ServerContext.ItemStackFactory;
            
            var mainInventory =
                serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId)
                    .MainOpenableInventory;
            
            //インベントリを満杯にする
            for (var i = 0; i < mainInventory.GetSlotSize(); i++)
                mainInventory.SetItem(i, itemStackFactory.Create(new ItemId(10), 1));
            
            //ブロックを設置
            worldBlock.TryAddBlock(ForUnitTestModBlockId.MachineId, new Vector3Int(0, 0), BlockDirection.North, out _);
            
            
            //プロトコルを使ってブロックを削除
            packet.GetPacketResponse(RemoveBlock(new Vector3Int(0, 0), PlayerId));
            
            
            //ブロックが削除できていないことを検証
            Assert.True(worldBlock.Exists(new Vector3Int(0, 0)));
        }
        
        
        private List<byte> RemoveBlock(Vector3Int pos, int playerId)
        {
            return MessagePackSerializer.Serialize(new RemoveBlockProtocolMessagePack(playerId, pos)).ToList();
        }
    }
}