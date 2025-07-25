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
using Server.Event.EventReceive;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using UnityEngine;
using static Server.Protocol.PacketResponse.EventProtocol;

namespace Tests.CombinedTest.Server.PacketTest.Event
{
    public class BaseCampEventTest
    {
        private const int PlayerId = 1;
        
        [Test]
        public void ItemDeliveryEventTest()
        {
            var (packetResponse, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            
            var worldBlockDataStore = ServerContext.WorldBlockDatastore;
            var itemStackFactory = ServerContext.ItemStackFactory;
            
            var position = new Vector3Int(10, 20);
            var baseCampBlockId = ForUnitTestModBlockId.BaseCamp1;
            
            // ベースキャンプブロックを設置
            worldBlockDataStore.TryAddBlock(baseCampBlockId, position, BlockDirection.North, out var baseCampBlock);
            var baseCampInventory = baseCampBlock.GetComponent<IBlockInventory>();
            
            // イベントリクエストを送信してイベント受信を開始
            packetResponse.GetPacketResponse(GetEventPacket());
            
            // アイテムを納品
            var deliveredItem = itemStackFactory.Create(new ItemId(1), 3);
            baseCampInventory.InsertItem(deliveredItem);
            
            // イベントパケットを取得
            var eventPackets = packetResponse.GetPacketResponse(GetEventPacket());
            var eventMessagePack = MessagePackSerializer.Deserialize<ResponseEventProtocolMessagePack>(eventPackets[0].ToArray());
            
            // 納品イベントが発生していることを確認
            Assert.AreEqual(1, eventMessagePack.Events.Count);
            
            var payLoad = eventMessagePack.Events[0].Payload;
            var updateEvent = MessagePackSerializer.Deserialize<OpenableBlockInventoryUpdateEventMessagePack>(payLoad);
            
            Assert.AreEqual(position.x, updateEvent.Position.X);
            Assert.AreEqual(position.y, updateEvent.Position.Y);
            Assert.AreEqual(1, updateEvent.Item.Id.AsPrimitive());
            Assert.AreEqual(3, updateEvent.Item.Count);
        }
        
        
        private List<byte> OpenCloseBlockInventoryPacket(Vector3Int pos, bool isOpen)
        {
            return MessagePackSerializer
                .Serialize(new BlockInventoryOpenCloseProtocol.BlockInventoryOpenCloseProtocolMessagePack(PlayerId, pos, isOpen)).ToList();
        }
        
        private List<byte> GetEventPacket()
        {
            return MessagePackSerializer.Serialize(new EventProtocolMessagePack(PlayerId)).ToList();
        }
    }
    
    // TODO: 実装時に削除 - 仮のインターフェース定義
    public interface IBaseCampComponent : IBlockComponent
    {
        bool IsCompleted();
        float GetProgress();
    }
    
    // TODO: 実装時に削除 - 仮のメッセージパック定義
    [MessagePackObject]
    public class BaseCampTransformEventMessagePack
    {
        [Key(0)] public Vector3Int Position { get; set; }
        [Key(1)] public BlockId OriginalBlockId { get; set; }
        [Key(2)] public BlockId TransformedBlockId { get; set; }
    }
}