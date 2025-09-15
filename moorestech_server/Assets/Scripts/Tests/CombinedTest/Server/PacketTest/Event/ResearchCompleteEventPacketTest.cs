using System.Linq;
using Core.Master;
using Game.Context;
using Game.PlayerInventory.Interface;
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
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            const int playerId = 1;

            // まずイベントキューを初期化（プレイヤー登録）
            packet.GetPacketResponse(EventTestUtil.EventRequestData(playerId));

            // 対象の研究を選定（消費無し優先）
            var all = MasterHolder.ResearchMaster.GetAllResearches();
            var target = all.FirstOrDefault(e => e.ConsumeItems == null || e.ConsumeItems.Length == 0) ?? all.First();

            // 必要であれば要求アイテムを投入
            if (target.ConsumeItems != null)
            {
                var inv = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(playerId);
                for (var i = 0; i < target.ConsumeItems.Length; i++)
                {
                    var req = target.ConsumeItems[i];
                    var stack = ServerContext.ItemStackFactory.Create(req.ItemGuid, req.ItemCount);
                    inv.MainOpenableInventory.SetItem(i, stack);
                }
            }

            // 研究完了をプロトコル経由で実行
            var req = new CompleteResearchProtocol.RequestCompleteResearchMessagePack(playerId, target.ResearchNodeGuid);
            packet.GetPacketResponse(MessagePackSerializer.Serialize(req).ToList());

            // イベントを取得
            var evBytes = packet.GetPacketResponse(EventTestUtil.EventRequestData(playerId));
            var ev = MessagePackSerializer.Deserialize<EventProtocol.ResponseEventProtocolMessagePack>(evBytes[0].ToArray());

            // 研究完了イベントを抽出
            var researchEvent = ev.Events.FirstOrDefault(e => e.Tag == ResearchCompleteEventPacket.EventTag);
            Assert.IsNotNull(researchEvent, "ResearchComplete event not found");

            var payload = MessagePackSerializer.Deserialize<ResearchCompleteEventPacket.ResearchCompleteEventMessagePack>(researchEvent.Payload);
            Assert.AreEqual(playerId, payload.PlayerId);
            Assert.AreEqual(target.ResearchNodeGuid.ToString(), payload.ResearchGuidStr);
        }
    }
}
