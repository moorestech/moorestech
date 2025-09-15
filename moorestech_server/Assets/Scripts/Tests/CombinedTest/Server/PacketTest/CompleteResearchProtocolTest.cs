using System;
using System.Linq;
using Core.Master;
using Game.Context;
using Game.PlayerInventory.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class CompleteResearchProtocolTest
    {
        [Test]
        public void CompleteResearchTest()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            
            const int playerId = 0;
            var researchGuid1 = Guid.Parse("cd05e30d-d599-46d3-a079-769113cbbf17"); // Research 1 - no prerequisites
            var researchGuid2 = Guid.Parse("7f1464a7-ba55-4b96-9257-cfdeddf5bbdd"); // Research 2 - requires Research 1
            var researchGuid4 = Guid.Parse("bf9bda9e-dace-43c4-9a33-75f248fd17f6"); // Research 4 - no prerequisites
            
            // プレイヤーインベントリにアイテムを追加
            var playerInventoryData = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(playerId);
            var itemId1 = MasterHolder.ItemMaster.GetItemId(Guid.Parse("00000000-0000-0000-1234-000000000001"));
            
            // Research 1の完了テスト（アイテムあり）
            // 必要なアイテムを配置（1個必要）
            var item = ServerContext.ItemStackFactory.Create(itemId1, 1);
            playerInventoryData.MainOpenableInventory.SetItem(0, item);
            
            // 研究を完了
            var requestData = MessagePackSerializer.Serialize(new CompleteResearchProtocol.RequestCompleteResearchMessagePack(playerId, researchGuid1)).ToList();
            var response = packet.GetPacketResponse(requestData);
            var responseData = MessagePackSerializer.Deserialize<CompleteResearchProtocol.ResponseCompleteResearchMessagePack>(response[0].ToArray());
            
            Assert.IsTrue(responseData.Success);
            Assert.AreEqual(researchGuid1.ToString(), responseData.CompletedResearchGuidStr);
            
            // アイテムが消費されたことを確認
            Assert.AreEqual(0, playerInventoryData.MainOpenableInventory.GetItem(0).Count);
            
            // 同じ研究をもう一度完了しようとすると失敗する
            requestData = MessagePackSerializer.Serialize(new CompleteResearchProtocol.RequestCompleteResearchMessagePack(playerId, researchGuid1)).ToList();
            response = packet.GetPacketResponse(requestData);
            responseData = MessagePackSerializer.Deserialize<CompleteResearchProtocol.ResponseCompleteResearchMessagePack>(response[0].ToArray());
            
            Assert.IsFalse(responseData.Success);
            
            // Research 2の完了テスト（前提条件あり）
            // 必要なアイテムを配置（1個必要）
            item = ServerContext.ItemStackFactory.Create(itemId1, 1);
            playerInventoryData.MainOpenableInventory.SetItem(1, item);
            
            requestData = MessagePackSerializer.Serialize(new CompleteResearchProtocol.RequestCompleteResearchMessagePack(playerId, researchGuid2)).ToList();
            response = packet.GetPacketResponse(requestData);
            responseData = MessagePackSerializer.Deserialize<CompleteResearchProtocol.ResponseCompleteResearchMessagePack>(response[0].ToArray());
            
            Assert.IsTrue(responseData.Success);
            Assert.AreEqual(researchGuid2.ToString(), responseData.CompletedResearchGuidStr);
            
            // Research 4の完了テスト（アイテム不足）
            // アイテムが1個だけの状態で2個必要な研究を試みる
            item = ServerContext.ItemStackFactory.Create(itemId1, 1);
            playerInventoryData.MainOpenableInventory.SetItem(2, item);
            
            requestData = MessagePackSerializer.Serialize(new CompleteResearchProtocol.RequestCompleteResearchMessagePack(playerId, researchGuid4)).ToList();
            response = packet.GetPacketResponse(requestData);
            responseData = MessagePackSerializer.Deserialize<CompleteResearchProtocol.ResponseCompleteResearchMessagePack>(response[0].ToArray());
            
            Assert.IsFalse(responseData.Success);
            
            // Research 4の完了テスト（アイテム十分）
            item = ServerContext.ItemStackFactory.Create(itemId1, 2);
            playerInventoryData.MainOpenableInventory.SetItem(3, item);
            
            requestData = MessagePackSerializer.Serialize(new CompleteResearchProtocol.RequestCompleteResearchMessagePack(playerId, researchGuid4)).ToList();
            response = packet.GetPacketResponse(requestData);
            responseData = MessagePackSerializer.Deserialize<CompleteResearchProtocol.ResponseCompleteResearchMessagePack>(response[0].ToArray());
            
            Assert.IsTrue(responseData.Success);
            Assert.AreEqual(researchGuid4.ToString(), responseData.CompletedResearchGuidStr);
        }
    }
}