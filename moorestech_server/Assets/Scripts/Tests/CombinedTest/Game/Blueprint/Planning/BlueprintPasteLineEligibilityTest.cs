using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.Blueprint;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using Server.Protocol.PacketResponse.Util.Blueprint.Planning;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Game.Blueprint.Planning
{
    public class BlueprintPasteLineEligibilityTest
    {
        [TestCase(false)]
        [TestCase(true)]
        public void 遠すぎる保存線は省略しブロックは置けるTest(bool chain)
        {
            var context = new BlueprintPastePlannerTestContext(true, 0);
            var id = chain ? ForUnitTestModBlockId.GearChainPole : ForUnitTestModBlockId.ElectricPoleId;
            var blueprint = BlueprintPastePlannerTestContext.Create(id, id);
            blueprint.Blocks[1].OffsetX = 100000;
            var lines = chain ? blueprint.Chains : blueprint.Wires;
            lines.Add(new BlueprintLineJsonObject(0, 1, chain ? BlueprintPastePlannerTestContext.ChainGuid : BlueprintPastePlannerTestContext.WireGuid));
            var plan = context.Plan(blueprint, 1, new Dictionary<ItemId, int>());
            Assert.AreEqual(BlueprintPasteCopyState.Placeable, plan.Copies[0].State);
            Assert.AreEqual(BlueprintPasteLineFailureReason.OutOfRange, plan.Copies[0].Draft.Lines.Single().FailureReason);
        }

        [Test]
        public void 電線端点に非電気ブロックは使えないTest()
        {
            var context = new BlueprintPastePlannerTestContext(true, 0);
            var blueprint = BlueprintPastePlannerTestContext.Create(ForUnitTestModBlockId.BlockId, ForUnitTestModBlockId.BlockId);
            blueprint.Wires.Add(new BlueprintLineJsonObject(0, 1, BlueprintPastePlannerTestContext.WireGuid));
            var plan = context.Plan(blueprint, 1, new Dictionary<ItemId, int>());
            Assert.AreEqual(BlueprintPasteLineFailureReason.InvalidTarget, plan.Copies[0].Draft.Lines.Single().FailureReason);
            Assert.IsTrue(plan.Copies[0].IsPlaced);
        }

        [Test]
        public void 保存電線の重複は一回分だけ受理するTest()
        {
            var context = new BlueprintPastePlannerTestContext(true, 0);
            var blueprint = BlueprintPastePlannerTestContext.Create(ForUnitTestModBlockId.ElectricPoleId, ForUnitTestModBlockId.ElectricPoleId);
            blueprint.Wires.Add(new BlueprintLineJsonObject(0, 1, BlueprintPastePlannerTestContext.WireGuid));
            blueprint.Wires.Add(new BlueprintLineJsonObject(1, 0, BlueprintPastePlannerTestContext.WireGuid));
            var lines = context.Plan(blueprint, 1, new Dictionary<ItemId, int>()).Copies[0].Draft.Lines;
            Assert.IsTrue(lines[0].IsConnectable);
            Assert.AreEqual(BlueprintPasteLineFailureReason.AlreadyConnected, lines[1].FailureReason);
        }

        [Test]
        public void 接続不能な重複チェーンの素材は要求しないTest()
        {
            var context = new BlueprintPastePlannerTestContext(true, 0);
            var id = ForUnitTestModBlockId.GearChainPole;
            var blueprint = BlueprintPastePlannerTestContext.Create(id, id);
            blueprint.Chains.Add(new BlueprintLineJsonObject(0, 1, BlueprintPastePlannerTestContext.ChainGuid));
            blueprint.Chains.Add(new BlueprintLineJsonObject(1, 0, BlueprintPastePlannerTestContext.ChainGuid));

            // 無料設置でも有料のチェーンは一本分だけ払う
            // Even with free block placement, pay for exactly one valid chain
            var draft = BlueprintPasteCopyBuilder.BuildUnobstructed(blueprint);
            Assert.IsTrue(draft.Lines[0].IsConnectable);
            Assert.AreEqual(BlueprintPasteLineFailureReason.AlreadyConnected, draft.Lines[1].FailureReason);
            var required = BlueprintPasteCostCalculator.CalcRequiredItems(new[] { draft }, context.Wallet, true);
            foreach (var material in draft.Lines[0].Materials)
            {
                Assert.AreEqual(material.Count, required.Single(row => row.itemId == material.ItemId).count);
            }
        }

        [Test]
        public void 保存チェーンの接続上限を累積して判定するTest()
        {
            var context = new BlueprintPastePlannerTestContext(true, 0);
            var id = ForUnitTestModBlockId.GearChainPole;
            var param = (GearChainPoleBlockParam)MasterHolder.BlockMaster.GetBlockMaster(id).BlockParam;
            var blueprint = BlueprintPastePlannerTestContext.Create(Enumerable.Repeat(id, param.MaxConnectionCount + 2).ToArray());
            for (var i = 1; i < blueprint.Blocks.Count; i++)
            {
                // 占有を見ない素材プランで接続予約だけを検証する
                // Isolate connection reservations in an unobstructed material plan
                blueprint.Blocks[i].OffsetX = 1;
                blueprint.Chains.Add(new BlueprintLineJsonObject(0, i, BlueprintPastePlannerTestContext.ChainGuid));
            }
            var draft = BlueprintPasteCopyBuilder.BuildUnobstructed(blueprint);
            Assert.AreEqual(param.MaxConnectionCount, draft.Lines.Count(line => line.IsConnectable));
            Assert.AreEqual(BlueprintPasteLineFailureReason.ConnectionLimit, draft.Lines.Last().FailureReason);
        }
    }
}
