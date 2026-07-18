using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using MessagePack;
using NUnit.Framework;
using Server.Boot;
using Server.Event.EventReceive;
using Server.Protocol.PacketResponse;
using Server.Util.MessagePack;
using Tests.Module.TestMod;
using UnityEngine;
using System;
using Server.Event.EventReceive.UnifiedInventoryEvent;
using Server.Protocol;

namespace Tests.CombinedTest.Server.PacketTest.Event
{
    /// <summary>
    ///     ブロックのインベントリが更新された時、イベントのパケットが更新されているかをテストする
    /// </summary>
    public class BlockInventoryUpdateEventPacketTest
    {
        private const int PlayerId = 3;
        private const short PacketId = 16;
        
        //正しくインベントリの情報が更新されたことを通知するパケットが送られるかチェックする
        [Test]
        public void BlockInventoryUpdatePacketTest()
        {
            var (packetResponse, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var sink = EventTestUtil.RegisterCaptureSink(serviceProvider, PlayerId);
            
            var worldBlockDataStore = ServerContext.WorldBlockDatastore;
            var itemStackFactory = ServerContext.ItemStackFactory;
            
            Vector3Int pos = new(5, 7);
            
            //ブロックをセットアップ
            worldBlockDataStore.TryAddBlock(ForUnitTestModBlockId.MachineId, pos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);
            var blockInventory = block.GetComponent<IBlockInventory>();
            sink.TakeAll();
            
            
            //インベントリを開く
            packetResponse.GetPacketResponse(OpenCloseBlockInventoryPacket(new Vector3Int(5, 7), true), new PacketResponseContext());
            //ブロックにアイテムを入れる
            blockInventory.SetItem(1, itemStackFactory.Create(new ItemId(4), 8));
            
            
            //イベントパケットを取得してチェック
            //Take the captured event packets and verify them
            var events = sink.TakeAll();
            Assert.AreEqual(1, events.Count);
            var payLoad = events[0].Payload;
            var data = MessagePackSerializer.Deserialize<UnifiedInventoryEventMessagePack>(payLoad);
            
            Assert.AreEqual(InventoryEventType.Update, data.EventType); // event type
            Assert.AreEqual(InventoryType.Block, data.Identifier.InventoryType); // inventory type
            Assert.AreEqual(1, data.Slot); // slot id
            Assert.AreEqual(4, data.Item.Id.AsPrimitive()); // item id
            Assert.AreEqual(8, data.Item.Count); // item count
            Assert.AreEqual(5, data.Identifier.BlockPosition.X); // x
            Assert.AreEqual(7, data.Identifier.BlockPosition.Y); // y
            
            
            //ブロックのインベントリを閉じる
            packetResponse.GetPacketResponse(OpenCloseBlockInventoryPacket(new Vector3Int(5, 7), false), new PacketResponseContext());
            
            //ブロックにアイテムを入れる
            blockInventory.SetItem(2, itemStackFactory.Create(new ItemId(4), 8));
            
            
            //パケットが送られていないことをチェック
            //イベントパケットを取得
            Assert.AreEqual(0, sink.TakeAll().Count);
        }
        
        
        // 複数のインベントリを同時にサブスクライブできることをテストする
        // Test that multiple inventories can be subscribed simultaneously
        [Test]
        public void MultipleInventoriesCanBeOpenedTest()
        {
            var (packetResponse, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var sink = EventTestUtil.RegisterCaptureSink(serviceProvider, PlayerId);

            var worldBlockDataStore = ServerContext.WorldBlockDatastore;
            var itemStackFactory = ServerContext.ItemStackFactory;

            // ブロック1をセットアップ
            // Setup block 1
            worldBlockDataStore.TryAddBlock(ForUnitTestModBlockId.MachineId, new Vector3Int(5, 7), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block1);

            // ブロック2をセットアップ
            // Setup block 2
            worldBlockDataStore.TryAddBlock(ForUnitTestModBlockId.MachineId, new Vector3Int(10, 20), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block2);
            sink.TakeAll();


            // 一つ目のブロックインベントリを開く
            // Open first block inventory
            packetResponse.GetPacketResponse(OpenCloseBlockInventoryPacket(new Vector3Int(5, 7), true), new PacketResponseContext());
            // 二つ目のブロックインベントリを開く
            // Open second block inventory
            packetResponse.GetPacketResponse(OpenCloseBlockInventoryPacket(new Vector3Int(10, 20), true), new PacketResponseContext());


            // 一つ目のブロックインベントリにアイテムを入れる
            // Add item to first block inventory
            var block1Inventory = block1.GetComponent<VanillaMachineBlockInventoryComponent>();
            block1Inventory.SetItem(2, itemStackFactory.Create(new ItemId(4), 8));


            // パケットが送られていることをチェック（複数サブスクリプション対応のため）
            // Check that packet is sent (multiple subscriptions are now supported)
            var events = sink.TakeAll();
            Assert.AreEqual(1, events.Count);

            // イベントの内容を検証
            // Verify event content
            var payLoad = events[0].Payload;
            var data = MessagePackSerializer.Deserialize<UnifiedInventoryEventMessagePack>(payLoad);
            Assert.AreEqual(InventoryEventType.Update, data.EventType);
            Assert.AreEqual(InventoryType.Block, data.Identifier.InventoryType);
            Assert.AreEqual(2, data.Slot);
            Assert.AreEqual(5, data.Identifier.BlockPosition.X);
            Assert.AreEqual(7, data.Identifier.BlockPosition.Y);


            // 二つ目のブロックインベントリにアイテムを入れる
            // Add item to second block inventory
            var block2Inventory = block2.GetComponent<VanillaMachineBlockInventoryComponent>();
            block2Inventory.SetItem(3, itemStackFactory.Create(new ItemId(5), 10));


            // パケットが送られていることをチェック
            // Check that packet is sent
            events = sink.TakeAll();
            Assert.AreEqual(1, events.Count);

            // イベントの内容を検証
            // Verify event content
            payLoad = events[0].Payload;
            data = MessagePackSerializer.Deserialize<UnifiedInventoryEventMessagePack>(payLoad);
            Assert.AreEqual(InventoryEventType.Update, data.EventType);
            Assert.AreEqual(InventoryType.Block, data.Identifier.InventoryType);
            Assert.AreEqual(3, data.Slot);
            Assert.AreEqual(10, data.Identifier.BlockPosition.X);
            Assert.AreEqual(20, data.Identifier.BlockPosition.Y);
        }
        
        
        private byte[] OpenCloseBlockInventoryPacket(Vector3Int pos, bool isOpen)
        {
            var identifier = InventoryIdentifierMessagePack.CreateBlockMessage(pos);
            return MessagePackSerializer
                .Serialize(new SubscribeInventoryProtocol.SubscribeInventoryRequestMessagePack(PlayerId, identifier, isOpen));
        }
    }
}
