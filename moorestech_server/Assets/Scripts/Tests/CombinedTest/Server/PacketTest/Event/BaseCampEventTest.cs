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
        public void BaseCampCompletionEventTest()
        {
            var (packetResponse, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            
            var worldBlockDataStore = ServerContext.WorldBlockDatastore;
            var itemStackFactory = ServerContext.ItemStackFactory;
            
            var position = new Vector3Int(10, 20);
            var baseCampBlockId = ForUnitTestModBlockId.BaseCamp1;
            
            // ベースキャンプブロックを設置
            worldBlockDataStore.TryAddBlock(baseCampBlockId, position, BlockDirection.North, out var baseCampBlock);
            var baseCampComponent = baseCampBlock.GetComponent<IBaseCampComponent>();
            var baseCampInventory = baseCampBlock.GetComponent<IBlockInventory>();
            
            // イベントリクエストを送信してイベント受信を開始
            packetResponse.GetPacketResponse(GetEventPacket());
            
            // アイテムを部分的に納品（まだ完了しない）
            var partialItem = itemStackFactory.Create(new ItemId(1), 3);
            baseCampInventory.InsertItem(partialItem);
            
            // この時点ではまだ完了していない
            Assert.IsFalse(baseCampComponent.IsCompleted());
            
            // イベントを消費
            packetResponse.GetPacketResponse(GetEventPacket());
            
            // 残りのアイテムを納品して完了させる
            var remainingItem = itemStackFactory.Create(new ItemId(1), 2);
            baseCampInventory.InsertItem(remainingItem);
            
            // 完了を確認
            Assert.IsTrue(baseCampComponent.IsCompleted());
            
            // 完了イベントパケットを取得
            var eventPackets = packetResponse.GetPacketResponse(GetEventPacket());
            var eventMessagePack = MessagePackSerializer.Deserialize<ResponseEventProtocolMessagePack>(eventPackets[0].ToArray());
            
            // 完了イベントが発生していることを確認
            Assert.GreaterOrEqual(eventMessagePack.Events.Count, 1);
            
            // 完了イベントを探す
            BaseCampCompletionEventMessagePack completionEvent = null;
            foreach (var evt in eventMessagePack.Events)
            {
                try
                {
                    completionEvent = MessagePackSerializer.Deserialize<BaseCampCompletionEventMessagePack>(evt.Payload);
                    break;
                }
                catch
                {
                    // 他のイベントタイプの場合は無視
                }
            }
            
            Assert.IsNotNull(completionEvent);
            Assert.AreEqual(position.x, completionEvent.Position.x);
            Assert.AreEqual(position.y, completionEvent.Position.y);
            Assert.AreEqual(baseCampBlockId, completionEvent.BlockId);
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
    public class BaseCampCompletionEventMessagePack
    {
        [Key(0)] public Vector3Int Position { get; set; }
        [Key(1)] public BlockId BlockId { get; set; }
    }
}