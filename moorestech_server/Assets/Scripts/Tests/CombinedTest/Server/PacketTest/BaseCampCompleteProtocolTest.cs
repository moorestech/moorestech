using System.Collections.Generic;
using System.Linq;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using MessagePack;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class BaseCampCompleteProtocolTest
    {
        [Test]
        public void BaseCampCompleteProtocolTest_Success()
        {
            var (packetResponse, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            
            var itemStackFactory = ServerContext.ItemStackFactory;
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            
            var position = new Vector3Int(10, 0, 10);
            var baseCampBlockId = ForUnitTestModBlockId.BaseCamp1;
            
            // ワールドに配置
            worldBlockDatastore.TryAddBlock(baseCampBlockId, position, BlockDirection.North, out var baseCampBlock);
            
            var baseCampComponent = baseCampBlock.GetComponent<IBaseCampComponent>();
            var baseCampInventory = baseCampBlock.GetComponent<IBlockInventory>();
            
            // 必要なアイテムを納品
            var requiredItemId = new ItemId(1);
            var requiredAmount = 5;
            baseCampInventory.InsertItem(itemStackFactory.Create(requiredItemId, requiredAmount));
            
            // 納品完了を確認
            Assert.IsTrue(baseCampComponent.IsCompleted());
            
            // 納品完了プロトコルを送信
            var completeRequest = MessagePackSerializer.Serialize(new CompleteBaseCampProtocolMessagePack(1, position)).ToList();
            packetResponse.GetPacketResponse(completeRequest);
            
            // ブロックが変換されたことを確認
            var transformedBlock = worldBlockDatastore.GetBlock(position);
            Assert.IsNotNull(transformedBlock);
            Assert.AreNotEqual(baseCampBlockId, transformedBlock.BlockId);
            Assert.AreEqual(ForUnitTestModBlockId.TransformedBlock, transformedBlock.BlockId);
        }
    }
    
    // TODO: 実装時に削除 - 仮のプロトコル定義
    [MessagePackObject]
    public class CompleteBaseCampProtocolMessagePack
    {
        [Key(0)] public int PlayerId { get; set; }
        [Key(1)] public Vector3Int Position { get; set; }
        
        public CompleteBaseCampProtocolMessagePack() { }
        
        public CompleteBaseCampProtocolMessagePack(int playerId, Vector3Int position)
        {
            PlayerId = playerId;
            Position = position;
        }
    }
    
    // TODO: 実装時に削除 - 仮のインターフェース定義
    public interface IBaseCampComponent : IBlockComponent
    {
        bool IsCompleted();
        float GetProgress();
    }
}