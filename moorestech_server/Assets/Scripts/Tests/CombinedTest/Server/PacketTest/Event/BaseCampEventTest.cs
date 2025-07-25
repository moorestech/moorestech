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
            var baseCampBlockId = MasterHolder.BlockMaster.GetBlockId(new System.Guid("5f8e8f90-0000-0000-0000-000000000001")); // TODO: 実際のIDに変更
            
            // ベースキャンプブロックを設置
            worldBlockDataStore.TryAddBlock(baseCampBlockId, position, BlockDirection.North, out var baseCampBlock);
            var baseCampInventory = baseCampBlock.GetComponent<IBlockInventory>();
            
            // インベントリを開く
            指摘：この送信は不要
            packetResponse.GetPacketResponse(OpenCloseBlockInventoryPacket(position, true));
            
            // イベントリクエストを送信
            指摘：この送信も同様に不要
            packetResponse.GetPacketResponse(GetEventPacket());
            
            // アイテムを納品
            var deliveredItem = itemStackFactory.Create(new ItemId(1), 3);
            baseCampInventory.InsertItem(deliveredItem);
            
            // イベントパケットを取得
            var eventPackets = packetResponse.GetPacketResponse(GetEventPacket());
            var eventMessagePack = MessagePackSerializer.Deserialize<ResponseEventProtocolMessagePack>(eventPackets[0].ToArray());
            
            // 納品イベントが発生していることを確認
            指摘：このチェックはOK
            Assert.AreEqual(1, eventMessagePack.Events.Count);
            
            var payLoad = eventMessagePack.Events[0].Payload;
            var updateEvent = MessagePackSerializer.Deserialize<OpenableBlockInventoryUpdateEventMessagePack>(payLoad);
            
            Assert.AreEqual(position.x, updateEvent.Position.X);
            Assert.AreEqual(position.y, updateEvent.Position.Y);
            Assert.AreEqual(1, updateEvent.Item.Id.AsPrimitive());
            Assert.AreEqual(3, updateEvent.Item.Count);
        }
        
        [Test]
        public void BlockTransformationEventTest()
        {
            var (packetResponse, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            
            var worldBlockDataStore = ServerContext.WorldBlockDatastore;
            var itemStackFactory = ServerContext.ItemStackFactory;
            
            var position = new Vector3Int(5, 5);
            var baseCampBlockId = MasterHolder.BlockMaster.GetBlockId(new System.Guid("5f8e8f90-0000-0000-0000-000000000001"));
            
            // ベースキャンプブロックを設置
            worldBlockDataStore.TryAddBlock(baseCampBlockId, position, BlockDirection.South, out var baseCampBlock);
            var baseCampComponent = baseCampBlock.GetComponent<IBaseCampComponent>();
            var baseCampInventory = baseCampBlock.GetComponent<IBlockInventory>();
            
            // イベントリクエストを送信
            packetResponse.GetPacketResponse(GetEventPacket());
            
            // 必要なアイテムをすべて納品してブロック変換をトリガー
            var requiredItems = new ItemId(1);
            var requiredAmount = 5;
            baseCampInventory.InsertItem(itemStackFactory.Create(requiredItems, requiredAmount));
            
            // ブロック変換が完了していることを確認
            指摘：完了と変換は別。完了の後、ユーザーが明示的に納品ボタンを押すことで初めて変換が行われる。
            Assert.IsTrue(baseCampComponent.IsCompleted());
            
            // イベントパケットを取得
            var eventPackets = packetResponse.GetPacketResponse(GetEventPacket());
            var eventMessagePack = MessagePackSerializer.Deserialize<ResponseEventProtocolMessagePack>(eventPackets[0].ToArray());
            
            // 変換イベントが発生していることを確認
            指摘：このテストそのものが不要
            Assert.GreaterOrEqual(eventMessagePack.Events.Count, 1);
            
            // 変換イベントを探す
            BaseCampTransformEventMessagePack transformEvent = null;
            foreach (var evt in eventMessagePack.Events)
            {
                try
                {
                    transformEvent = MessagePackSerializer.Deserialize<BaseCampTransformEventMessagePack>(evt.Payload);
                    break;
                }
                catch
                {
                    // 他のイベントタイプの場合は無視
                }
            }
            
            Assert.IsNotNull(transformEvent);
            Assert.AreEqual(position.x, transformEvent.Position.x);
            Assert.AreEqual(position.y, transformEvent.Position.y);
            Assert.AreEqual(baseCampBlockId, transformEvent.OriginalBlockId);
            Assert.AreNotEqual(baseCampBlockId, transformEvent.TransformedBlockId);
        }
        
        [Test]
        指摘：いったんこのイベントは不要なのでこのテストを削除
        public void ProgressUpdateEventTest()
        {
            var (packetResponse, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            
            var worldBlockDataStore = ServerContext.WorldBlockDatastore;
            var itemStackFactory = ServerContext.ItemStackFactory;
            
            var position = new Vector3Int(15, 15);
            var baseCampBlockId = MasterHolder.BlockMaster.GetBlockId(new System.Guid("5f8e8f90-0000-0000-0000-000000000002")); // 複数アイテム要求版
            
            // ベースキャンプブロックを設置
            worldBlockDataStore.TryAddBlock(baseCampBlockId, position, BlockDirection.East, out var baseCampBlock);
            var baseCampInventory = baseCampBlock.GetComponent<IBlockInventory>();
            
            // インベントリを開く
            packetResponse.GetPacketResponse(OpenCloseBlockInventoryPacket(position, true));
            
            // イベントリクエストを送信
            packetResponse.GetPacketResponse(GetEventPacket());
            
            // 複数回に分けてアイテムを納品し、進捗更新イベントを確認
            var itemsToDeliver = new List<(ItemId, int)>
            {
                (new ItemId(1), 2),
                (new ItemId(2), 3),
                (new ItemId(3), 1)
            };
            
            foreach (var (itemId, count) in itemsToDeliver)
            {
                baseCampInventory.InsertItem(itemStackFactory.Create(itemId, count));
                
                // イベントパケットを取得
                var eventPackets = packetResponse.GetPacketResponse(GetEventPacket());
                var eventMessagePack = MessagePackSerializer.Deserialize<ResponseEventProtocolMessagePack>(eventPackets[0].ToArray());
                
                // 進捗更新イベントが発生していることを確認
                Assert.GreaterOrEqual(eventMessagePack.Events.Count, 1);
            }
        }
        
        [Test]
        指摘：このテストも不要
        public void NoEventWhenInventoryClosedTest()
        {
            var (packetResponse, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            
            var worldBlockDataStore = ServerContext.WorldBlockDatastore;
            var itemStackFactory = ServerContext.ItemStackFactory;
            
            var position = new Vector3Int(30, 30);
            var baseCampBlockId = MasterHolder.BlockMaster.GetBlockId(new System.Guid("5f8e8f90-0000-0000-0000-000000000001"));
            
            // ベースキャンプブロックを設置
            worldBlockDataStore.TryAddBlock(baseCampBlockId, position, BlockDirection.West, out var baseCampBlock);
            var baseCampInventory = baseCampBlock.GetComponent<IBlockInventory>();
            
            // インベントリを開かない状態でアイテムを納品
            baseCampInventory.InsertItem(itemStackFactory.Create(new ItemId(1), 3));
            
            // イベントパケットを取得
            var eventPackets = packetResponse.GetPacketResponse(GetEventPacket());
            var eventMessagePack = MessagePackSerializer.Deserialize<ResponseEventProtocolMessagePack>(eventPackets[0].ToArray());
            
            // インベントリが閉じているのでイベントは発生しない
            Assert.AreEqual(0, eventMessagePack.Events.Count);
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
        BlockId GetTransformedBlockId();
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