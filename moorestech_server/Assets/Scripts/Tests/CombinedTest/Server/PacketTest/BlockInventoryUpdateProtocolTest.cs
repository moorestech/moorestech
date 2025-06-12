using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using MessagePack;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Server.Protocol
{
    public class BlockInventoryUpdateProtocolTest
    {
        private const int PlayerId = 1;
        
        [Test]
        public void RequestBlockInventoryTest()
        {
            var (packetResponse, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            var worldContext = ServerContext.WorldBlockDatastore;
            
            // インベントリ付きのブロックを配置（チェストを使用）
            worldContext.TryAddBlock(ForUnitTestModBlockId.ChestId, new Vector3Int(5, 0, 5), BlockDirection.North, out var chestBlock);
            
            // インベントリにアイテムを追加
            var blockInventory = chestBlock.GetComponent<IBlockInventory>();
            var itemStackFactory = ServerContext.ItemStackFactory;
            blockInventory.SetItem(0, itemStackFactory.Create(ForUnitTestItemId.ItemId1, 3));
            blockInventory.SetItem(1, itemStackFactory.Create(ForUnitTestItemId.ItemId2, 5));
            
            // プロトコルをテスト
            var request = new BlockInventoryUpdateProtocol.RequestBlockInventoryUpdateProtocolMessagePack(new Vector3Int(5, 0, 5));
            var requestBytes = MessagePackSerializer.Serialize(request).ToList();
            
            var responseBytes = packetResponse.GetPacketResponse(requestBytes);
            var response = MessagePackSerializer.Deserialize<BlockInventoryUpdateProtocol.BlockInventoryUpdateResponseProtocolMessagePack>(responseBytes[0].ToArray());
            
            // レスポンスを検証
            Assert.NotNull(response);
            Assert.AreEqual(5, response.Items.Count); // チェストは5スロット
            
            // スロット0の検証
            Assert.AreEqual(ForUnitTestItemId.ItemId1.AsPrimitive(), response.Items[0].Id.AsPrimitive());
            Assert.AreEqual(3, response.Items[0].Count);
            
            // スロット1の検証
            Assert.AreEqual(ForUnitTestItemId.ItemId2.AsPrimitive(), response.Items[1].Id.AsPrimitive());
            Assert.AreEqual(5, response.Items[1].Count);
            
            // 残りのスロットは空であることを確認
            for (int i = 2; i < 5; i++)
            {
                Assert.AreEqual(ItemMaster.EmptyItemId.AsPrimitive(), response.Items[i].Id.AsPrimitive());
                Assert.AreEqual(0, response.Items[i].Count);
            }
        }
        
        [Test]
        public void RequestEmptyBlockPositionTest()
        {
            var (packetResponse, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            
            // 空の位置をリクエスト
            var request = new BlockInventoryUpdateProtocol.RequestBlockInventoryUpdateProtocolMessagePack(new Vector3Int(10, 0, 10));
            var requestBytes = MessagePackSerializer.Serialize(request).ToList();
            
            var responseBytes = packetResponse.GetPacketResponse(requestBytes);
            var response = MessagePackSerializer.Deserialize<BlockInventoryUpdateProtocol.BlockInventoryUpdateResponseProtocolMessagePack>(responseBytes[0].ToArray());
            
            // レスポンスを検証
            Assert.NotNull(response);
            Assert.AreEqual(0, response.Items.Count);
        }
        
        [Test]
        public void RequestBlockWithoutInventoryTest()
        {
            var (packetResponse, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            var worldContext = ServerContext.WorldBlockDatastore;
            
            // インベントリを持たないブロックを配置
            worldContext.TryAddBlock(ForUnitTestModBlockId.BlockId, new Vector3Int(3, 0, 3), BlockDirection.North, out var block);
            
            // プロトコルをテスト
            var request = new BlockInventoryUpdateProtocol.RequestBlockInventoryUpdateProtocolMessagePack(new Vector3Int(3, 0, 3));
            var requestBytes = MessagePackSerializer.Serialize(request).ToList();
            
            var responseBytes = packetResponse.GetPacketResponse(requestBytes);
            var response = MessagePackSerializer.Deserialize<BlockInventoryUpdateProtocol.BlockInventoryUpdateResponseProtocolMessagePack>(responseBytes[0].ToArray());
            
            // レスポンスを検証
            Assert.NotNull(response);
            Assert.AreEqual(0, response.Items.Count);
        }
        
        [Test]
        public void EmptyInventorySlotsTest()
        {
            var (packetResponse, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            var worldContext = ServerContext.WorldBlockDatastore;
            
            // インベントリ付きのブロックを配置（アイテムなし）
            worldContext.TryAddBlock(ForUnitTestModBlockId.MachineId, new Vector3Int(7, 0, 7), BlockDirection.North, out var machineBlock);
            
            // プロトコルをテスト
            var request = new BlockInventoryUpdateProtocol.RequestBlockInventoryUpdateProtocolMessagePack(new Vector3Int(7, 0, 7));
            var requestBytes = MessagePackSerializer.Serialize(request).ToList();
            
            var responseBytes = packetResponse.GetPacketResponse(requestBytes);
            var response = MessagePackSerializer.Deserialize<BlockInventoryUpdateProtocol.BlockInventoryUpdateResponseProtocolMessagePack>(responseBytes[0].ToArray());
            
            // レスポンスを検証
            Assert.NotNull(response);
            Assert.AreEqual(5, response.Items.Count); // マシンは5スロット（input 2 + output 3）
            
            // 空のスロットの検証
            foreach (var item in response.Items)
            {
                Assert.AreEqual(ItemMaster.EmptyItemId.AsPrimitive(), item.Id.AsPrimitive());
                Assert.AreEqual(0, item.Count);
            }
        }
    }
}