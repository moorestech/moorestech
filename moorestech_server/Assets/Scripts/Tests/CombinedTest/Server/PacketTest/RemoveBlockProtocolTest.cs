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
using System;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class RemoveBlockProtocolTest
    {
        private const int MachineBlockId = 1;
        private const int PlayerId = 0;
        
        [Test]
        public void RemoveTest()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var worldBlock = ServerContext.WorldBlockDatastore;
            var itemStackFactory = ServerContext.ItemStackFactory;

            var playerInventoryData = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId);

            // 削除するためのブロックの生成
            // Create block to be removed
            worldBlock.TryAddBlock(ForUnitTestModBlockId.MachineId, new Vector3Int(0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);
            var blockInventory = block.GetComponent<IBlockInventory>();
            blockInventory.InsertItem(itemStackFactory.Create(new ItemId(10), 7), InsertItemContext.Empty);
            var blockElement = MasterHolder.BlockMaster.GetBlockMaster(block.BlockId);

            // プロトコルを使ってブロックを削除
            // Remove block using protocol
            packet.GetPacketResponse(RemoveBlock(new Vector3Int(0, 0), PlayerId));

            // 削除したブロックがワールドに存在しないことを確認
            // Verify removed block no longer exists in world
            Assert.False(worldBlock.Exists(new Vector3Int(0, 0)));

            // アイテムの挿入順序はブロック自体→ブロックインベントリの順
            // Insertion order is block item first, then block inventory items
            // スロット0にブロック自体のアイテムが入る
            // Block item goes to slot 0
            var blockItemId = MasterHolder.ItemMaster.GetItemId(blockElement.ItemGuid);
            Assert.AreEqual(blockItemId, playerInventoryData.MainOpenableInventory.GetItem(0).Id);
            Assert.AreEqual(1, playerInventoryData.MainOpenableInventory.GetItem(0).Count);

            // スロット1にブロックインベントリ内のアイテムが入る
            // Block inventory items go to slot 1
            Assert.AreEqual(10, playerInventoryData.MainOpenableInventory.GetItem(1).Id.AsPrimitive());
            Assert.AreEqual(7, playerInventoryData.MainOpenableInventory.GetItem(1).Count);
        }
        
        
        // インベントリに全て入りきらない場合はブロックが削除されないことのテスト
        // Test that block is not removed when inventory cannot fit all items
        [Test]
        public void InventoryFullToRemoveBlockSomeItemRemainTest()
        {
            var (packet, serviceProvider) =
                new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var worldBlock = ServerContext.WorldBlockDatastore;
            var itemStackFactory = ServerContext.ItemStackFactory;

            var mainInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).MainOpenableInventory;

            // インベントリの2つのスロットを残してインベントリを満杯にする
            // Fill inventory except for 2 slots
            for (var i = 2; i < mainInventory.GetSlotSize(); i++)
                mainInventory.SetItem(i, itemStackFactory.Create(new ItemId(10), 1));

            // 一つ目のスロットにはID3の最大スタック数から1個少ないアイテムを入れる
            // First slot has ID3 items at max stack - 1
            var id3MaxStack = MasterHolder.ItemMaster.GetItemMaster(new ItemId(3)).MaxStack;
            mainInventory.SetItem(0, itemStackFactory.Create(new ItemId(3), id3MaxStack - 1));
            // 二つめのスロットにはID4のアイテムを1つ入れておく
            // Second slot has 1 ID4 item
            mainInventory.SetItem(1, itemStackFactory.Create(new ItemId(4), 1));

            // 削除するためのブロックを設置
            // Place block to be removed
            worldBlock.TryAddBlock(ForUnitTestModBlockId.MachineId, new Vector3Int(0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);
            var blockInventory = block.GetComponent<IBlockInventory>();
            // ブロックにはID3のアイテムを2個と、ID4のアイテムを5個入れる
            // Block contains 2 ID3 items and 5 ID4 items
            blockInventory.SetItem(0, itemStackFactory.Create(new ItemId(3), 2));
            blockInventory.SetItem(1, itemStackFactory.Create(new ItemId(4), 5));

            // プロトコルを使ってブロックを削除
            // Try to remove block using protocol
            packet.GetPacketResponse(RemoveBlock(new Vector3Int(0, 0), PlayerId));

            // 新しい仕様：全てのアイテムが入らない場合はブロックは削除されない
            // New spec: Block is not removed if not all items can fit
            Assert.True(worldBlock.Exists(new Vector3Int(0, 0)));

            // プレイヤーのインベントリは変更されていないことを確認
            // Verify player inventory is unchanged
            Assert.AreEqual(itemStackFactory.Create(new ItemId(3), id3MaxStack - 1), mainInventory.GetItem(0));
            Assert.AreEqual(itemStackFactory.Create(new ItemId(4), 1), mainInventory.GetItem(1));

            // ブロックのインベントリも変更されていないことを確認
            // Verify block inventory is unchanged
            Assert.AreEqual(itemStackFactory.Create(new ItemId(3), 2), blockInventory.GetItem(0));
            Assert.AreEqual(itemStackFactory.Create(new ItemId(4), 5), blockInventory.GetItem(1));
        }
        
        //ブロックの中にアイテムはないけど、プレイヤーのインベントリが満杯でブロックを破壊できない時のテスト
        [Test]
        public void InventoryFullToCantRemoveBlockTest()
        {
            var (packet, serviceProvider) =
                new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var worldBlock = ServerContext.WorldBlockDatastore;
            var itemStackFactory = ServerContext.ItemStackFactory;
            
            var mainInventory =
                serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId)
                    .MainOpenableInventory;
            
            //インベントリを満杯にする
            for (var i = 0; i < mainInventory.GetSlotSize(); i++)
                mainInventory.SetItem(i, itemStackFactory.Create(new ItemId(10), 1));
            
            //ブロックを設置
            worldBlock.TryAddBlock(ForUnitTestModBlockId.MachineId, new Vector3Int(0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            
            
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
