using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.Block.Interface;
using Game.Blueprint;
using Game.Construction;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse.Util.Blueprint.Planning;
using Tests.Module.TestMod;
using UniRx;
using UnityEngine;

namespace Tests.CombinedTest.Game.Blueprint.Planning
{
    public class BlueprintPastePlannerLineTest
    {
        [TestCase(false)]
        [TestCase(true)]
        public void 未解放線種は無料設置でも重なりで省略されてもNotUnlockedTest(bool overlap)
        {
            var context = new BlueprintPastePlannerTestContext(true, 0);
            var blueprint = WiredBlueprint();
            context.World.Locked.Add(BlueprintPastePlannerTestContext.WireGuid);
            if (overlap) context.World.Overlaps.Add(Vector3Int.zero);

            var result = context.Plan(blueprint, 1, new Dictionary<ItemId, int>());
            Assert.AreEqual(BlueprintPasteCopyState.NotUnlocked, result.Copies[0].State);
        }

        [Test]
        public void 片端が重なる配線は解決されないTest()
        {
            var context = new BlueprintPastePlannerTestContext(true, 0);
            context.World.Overlaps.Add(Vector3Int.zero);
            var result = context.Plan(WiredBlueprint(), 1, new Dictionary<ItemId, int>());
            Assert.IsTrue(result.Copies[0].IsPlaced);
            Assert.IsEmpty(result.Copies[0].Draft.Lines);
            Assert.AreEqual(1, result.Copies[0].Draft.OverlappingEndpointLineCount);
        }

        [Test]
        public void 配線素材も総素材に入るTest()
        {
            var context = new BlueprintPastePlannerTestContext(false, 0);
            var held = BlueprintPastePlannerTestContext.BlockCosts(ForUnitTestModBlockId.ElectricPoleId, 2);
            var result = context.Plan(WiredBlueprint(), 1, held);

            Assert.AreEqual(BlueprintPasteCopyState.MaterialShortage, result.Copies[0].State);
            var line = result.Copies[0].Draft.Lines.Single();
            Assert.IsNotEmpty(line.Materials);
            Assert.IsTrue(line.Materials.Any(material => result.ShortageRequirements.Any(row =>
                row.itemId == material.ItemId && row.held < row.required)));
        }

        [Test]
        public void 支払い免除なら建設素材と電線素材が無くても置けるTest()
        {
            var context = new BlueprintPastePlannerTestContext(true, 0);
            var result = context.Plan(WiredBlueprint(), 1, new Dictionary<ItemId, int>());
            Assert.IsTrue(result.IsPaymentWaived);
            Assert.IsTrue(result.Copies[0].IsPlaced);
            Assert.AreEqual(1, result.Copies[0].Draft.Lines.Count);
        }

        [Test]
        public void 支払い免除でもチェーン素材は必要Test()
        {
            var context = new BlueprintPastePlannerTestContext(true, 0);
            var block = ForUnitTestModBlockId.GearChainPole;
            var blueprint = BlueprintPastePlannerTestContext.Create(block, block);
            blueprint.Chains.Add(new BlueprintLineJsonObject(0, 1, BlueprintPastePlannerTestContext.ChainGuid));
            var result = context.Plan(blueprint, 1, new Dictionary<ItemId, int>());
            Assert.AreEqual(BlueprintPasteCopyState.MaterialShortage, result.Copies[0].State);

            // チェーン代だけ渡せば無料設置中の建設分は要求しない
            // Supplying only chain materials suffices while block placement is free
            var held = result.Copies[0].Draft.Lines.Single().Materials.ToDictionary(cost => cost.ItemId, cost => cost.Count);
            Assert.IsTrue(context.Plan(blueprint, 1, held).Copies[0].IsPlaced);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void 回転後の端点を元のindexから解決するTest(int rotation)
        {
            var context = new BlueprintPastePlannerTestContext(true, 0);
            var origins = new[] { new BlueprintPasteOrigin(new Vector3Int(7, 2, 5), true) };
            var plan = BlueprintPastePlanner.Plan(WiredBlueprint(), origins, rotation, context.World,
                context.Wallet, new Dictionary<ItemId, int>());
            var draft = plan.Copies[0].Draft;
            var line = draft.Lines.Single();
            Assert.AreEqual(0, draft.Elements[line.ElementIndexA].BlockIndex);
            Assert.AreEqual(1, draft.Elements[line.ElementIndexB].BlockIndex);
            Assert.AreEqual(draft.Elements[0].Position, line.PositionA);
            Assert.AreEqual(draft.Elements[1].Position, line.PositionB);
            Assert.AreEqual(2f, Vector3Int.Distance(line.PositionA, line.PositionB));
        }

        [Test]
        public void 欠損ブロックで端点indexがずれても残る配線を復元するTest()
        {
            var context = new BlueprintPastePlannerTestContext(true, 0);
            var blueprint = WiredBlueprint();
            blueprint.Blocks.Insert(0, new BlueprintBlockJsonObject(Vector3Int.zero, Guid.NewGuid().ToString(),
                (int)BlockDirection.North, new Dictionary<string, string>()));
            blueprint.Wires.Clear();
            blueprint.Wires.Add(new BlueprintLineJsonObject(0, 1, BlueprintPastePlannerTestContext.WireGuid));
            blueprint.Wires.Add(new BlueprintLineJsonObject(1, 2, BlueprintPastePlannerTestContext.WireGuid));

            // マスタ欠損の線だけを省略し、保存indexは詰めない
            // Skip only the missing endpoint line without compacting saved indices
            var draft = BlueprintPasteCopyBuilder.BuildUnobstructed(blueprint);
            Assert.AreEqual(2, draft.Elements.Count);
            Assert.AreEqual(1, draft.MissingEndpointLineCount);
            var line = draft.Lines.Single();
            Assert.AreEqual(1, draft.Elements[line.ElementIndexA].BlockIndex);
            Assert.AreEqual(2, draft.Elements[line.ElementIndexB].BlockIndex);
        }

        [Test]
        public void 未知線種は省略理由を残しブロックの貼り付けを妨げないTest()
        {
            var context = new BlueprintPastePlannerTestContext(true, 0);
            var blueprint = WiredBlueprint();
            blueprint.Wires.Clear();
            blueprint.Wires.Add(new BlueprintLineJsonObject(0, 1, Guid.NewGuid()));

            var plan = context.Plan(blueprint, 1, new Dictionary<ItemId, int>());

            Assert.AreEqual(BlueprintPasteCopyState.Placeable, plan.Copies[0].State);
            Assert.IsEmpty(plan.Copies[0].Draft.Lines);
            Assert.AreEqual(1, plan.Copies[0].Draft.UnknownConnectToolLineCount);
        }

        private static BlueprintJsonObject WiredBlueprint()
        {
            var block = ForUnitTestModBlockId.ElectricPoleId;
            var blueprint = BlueprintPastePlannerTestContext.Create(block, block);
            blueprint.Wires.Add(new BlueprintLineJsonObject(0, 1, BlueprintPastePlannerTestContext.WireGuid));
            return blueprint;
        }
    }
}
