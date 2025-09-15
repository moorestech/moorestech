using System;
using System.Linq;
using Core.Master;
using Game.Context;
using Game.PlayerInventory.Interface;
using Game.Research;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Event.EventReceive;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;

namespace Tests.CombinedTest.Server.PacketTest.Event
{
    public class ResearchCompleteEventPacketTest
    {
        [Test]
        public void ResearchCompleteToEventPacketTest()
        {
            var (packetResponse, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            const int playerId = 0;
            var researchGuid1 = Guid.Parse("cd05e30d-d599-46d3-a079-769113cbbf17"); // Research 1 - no prerequisites
            var researchGuid2 = Guid.Parse("7f1464a7-ba55-4b96-9257-cfdeddf5bbdd"); // Research 2 - requires Research 1

            // イベントがないことを確認する
            var response = packetResponse.GetPacketResponse(EventTestUtil.EventRequestData(playerId));
            var eventMessagePack = MessagePackSerializer.Deserialize<EventProtocol.ResponseEventProtocolMessagePack>(response[0].ToArray());
            Assert.AreEqual(0, eventMessagePack.Events.Count);

            // プレイヤーインベントリにアイテムを追加
            var playerInventoryData = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(playerId);
            var itemId1 = MasterHolder.ItemMaster.GetItemId(Guid.Parse("00000000-0000-0000-1234-000000000001"));

            // Research 1を完了させる
            // 必要なアイテムを配置（1個必要）
            var item = ServerContext.ItemStackFactory.Create(itemId1, 1);
            playerInventoryData.MainOpenableInventory.SetItem(0, item);

            // 研究を完了
            var researchDataStore = serviceProvider.GetService<IResearchDataStore>();
            var result = researchDataStore.CompleteResearch(researchGuid1, playerId);
            Assert.IsTrue(result);

            // イベントを受け取り、テストする
            response = packetResponse.GetPacketResponse(EventTestUtil.EventRequestData(playerId));
            eventMessagePack = MessagePackSerializer.Deserialize<EventProtocol.ResponseEventProtocolMessagePack>(response[0].ToArray());

            // イベントがあることを確認する
            Assert.AreEqual(1, eventMessagePack.Events.Count);

            // 研究完了イベントを確認
            var researchCompleteEvent = eventMessagePack.Events[0];
            Assert.AreEqual(ResearchCompleteEventPacket.EventTag, researchCompleteEvent.Tag);

            var researchCompleteData = MessagePackSerializer.Deserialize<ResearchCompleteEventPacket.ResearchCompleteEventMessagePack>(researchCompleteEvent.Payload);
            Assert.AreEqual(playerId, researchCompleteData.PlayerId);
            Assert.AreEqual(researchGuid1.ToString(), researchCompleteData.ResearchGuidStr);

            // Research 2を完了させる（前提条件付き）
            item = ServerContext.ItemStackFactory.Create(itemId1, 1);
            playerInventoryData.MainOpenableInventory.SetItem(1, item);

            result = researchDataStore.CompleteResearch(researchGuid2, playerId);
            Assert.IsTrue(result);

            // イベントを受け取り、テストする
            response = packetResponse.GetPacketResponse(EventTestUtil.EventRequestData(playerId));
            eventMessagePack = MessagePackSerializer.Deserialize<EventProtocol.ResponseEventProtocolMessagePack>(response[0].ToArray());

            // 新しいイベントがあることを確認する
            Assert.AreEqual(1, eventMessagePack.Events.Count);

            // Research 2の完了イベントを確認
            researchCompleteEvent = eventMessagePack.Events[0];
            Assert.AreEqual(ResearchCompleteEventPacket.EventTag, researchCompleteEvent.Tag);

            researchCompleteData = MessagePackSerializer.Deserialize<ResearchCompleteEventPacket.ResearchCompleteEventMessagePack>(researchCompleteEvent.Payload);
            Assert.AreEqual(playerId, researchCompleteData.PlayerId);
            Assert.AreEqual(researchGuid2.ToString(), researchCompleteData.ResearchGuidStr);
        }
    }
}
