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
            const int playerId = 1;

            // 対象の研究ノードを選定（できれば消費アイテム無し、無ければ最初の要素）
            var all = MasterHolder.ResearchMaster.GetAllResearches();
            var target = all.FirstOrDefault(e => e.ConsumeItems == null || e.ConsumeItems.Length == 0) ?? all.First();

            // 必要であればインベントリに要求アイテムを投入
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

            // 完了リクエストを送信
            var req = new CompleteResearchProtocol.RequestCompleteResearchMessagePack(playerId, target.ResearchNodeGuid);
            var resBytes = packet.GetPacketResponse(MessagePackSerializer.Serialize(req).ToList());
            var res = MessagePackSerializer.Deserialize<CompleteResearchProtocol.ResponseCompleteResearchMessagePack>(resBytes[0].ToArray());

            Assert.IsTrue(res.Success);
            Assert.AreEqual(target.ResearchNodeGuid.ToString(), res.CompletedResearchGuidStr);
        }
    }
}
