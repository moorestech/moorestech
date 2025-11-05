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
using static Server.Protocol.PacketResponse.EventProtocol;
using System;
using Server.Event.EventReceive.UnifiedInventoryEvent;

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
            
            var worldBlockDataStore = ServerContext.WorldBlockDatastore;
            var itemStackFactory = ServerContext.ItemStackFactory;
            
            Vector3Int pos = new(5, 7);
            
            //ブロックをセットアップ
            worldBlockDataStore.TryAddBlock(ForUnitTestModBlockId.MachineId, pos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);
            var blockInventory = block.GetComponent<IBlockInventory>();
            
            
            //インベントリを開く
            packetResponse.GetPacketResponse(OpenCloseBlockInventoryPacket(new Vector3Int(5, 7), true));
            //ブロックにアイテムを入れる
            blockInventory.SetItem(1, itemStackFactory.Create(new ItemId(4), 8));
            
            
            //パケットが送られていることをチェック
            //イベントパケットを取得
            List<List<byte>> eventPacket = packetResponse.GetPacketResponse(GetEventPacket());
            
            
            var eventMessagePack = MessagePackSerializer.Deserialize<ResponseEventProtocolMessagePack>(eventPacket[0].ToArray());
            //イベントパケットをチェック
            Assert.AreEqual(1, eventMessagePack.Events.Count);
            var payLoad = eventMessagePack.Events[0].Payload;
            var data = MessagePackSerializer.Deserialize<UnifiedInventoryEventMessagePack>(payLoad);
            
            Assert.AreEqual(InventoryEventType.Update, data.EventType); // event type
            Assert.AreEqual(InventoryType.Block, data.Identifier.InventoryType); // inventory type
            Assert.AreEqual(1, data.Slot); // slot id
            Assert.AreEqual(4, data.Item.Id.AsPrimitive()); // item id
            Assert.AreEqual(8, data.Item.Count); // item count
            Assert.AreEqual(5, data.Identifier.BlockPosition.X); // x
            Assert.AreEqual(7, data.Identifier.BlockPosition.Y); // y
            
            
            //ブロックのインベントリを閉じる
            packetResponse.GetPacketResponse(OpenCloseBlockInventoryPacket(new Vector3Int(5, 7), false));
            
            //ブロックにアイテムを入れる
            blockInventory.SetItem(2, itemStackFactory.Create(new ItemId(4), 8));
            
            
            //パケットが送られていないことをチェック
            //イベントパケットを取得
            eventPacket = packetResponse.GetPacketResponse(GetEventPacket());
            eventMessagePack = MessagePackSerializer.Deserialize<ResponseEventProtocolMessagePack>(eventPacket[0].ToArray());
            Assert.AreEqual(0, eventMessagePack.Events.Count);
        }
        
        
        //インベントリが開けるのは１つまでであることをテストする
        [Test]
        public void OnlyOneInventoryCanBeOpenedTest()
        {
            var (packetResponse, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            
            var worldBlockDataStore = ServerContext.WorldBlockDatastore;
            var itemStackFactory = ServerContext.ItemStackFactory;
            
            //ブロック1をセットアップ
            worldBlockDataStore.TryAddBlock(ForUnitTestModBlockId.MachineId, new Vector3Int(5, 7), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block1);
            
            //ブロック2をセットアップ
            worldBlockDataStore.TryAddBlock(ForUnitTestModBlockId.MachineId, new Vector3Int(10, 20), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block2);
            
            
            //一つ目のブロックインベントリを開く
            packetResponse.GetPacketResponse(OpenCloseBlockInventoryPacket(new Vector3Int(5, 7), true));
            //二つ目のブロックインベントリを開く
            packetResponse.GetPacketResponse(OpenCloseBlockInventoryPacket(new Vector3Int(10, 20), true));
            
            
            //一つ目のブロックインベントリにアイテムを入れる
            var block1Inventory = block1.GetComponent<VanillaMachineBlockInventoryComponent>();
            block1Inventory.SetItem(2, itemStackFactory.Create(new ItemId(4), 8));
            
            
            //パケットが送られていないことをチェック
            List<List<byte>> response = packetResponse.GetPacketResponse(GetEventPacket());
            var eventMessagePack = MessagePackSerializer.Deserialize<ResponseEventProtocolMessagePack>(response[0].ToArray());
            Assert.AreEqual(0, eventMessagePack.Events.Count);
        }
        
        
        private List<byte> OpenCloseBlockInventoryPacket(Vector3Int pos, bool isOpen)
        {
            var identifier = InventoryIdentifierMessagePack.CreateBlockMessage(pos);
            return MessagePackSerializer
                .Serialize(new SubscribeInventoryProtocol.SubscribeInventoryRequestMessagePack(PlayerId, InventoryType.Block, identifier, isOpen)).ToList();
        }
        
        private List<byte> GetEventPacket()
        {
            return MessagePackSerializer.Serialize(new EventProtocolMessagePack(PlayerId)).ToList();
        }
    }
}
