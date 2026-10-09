using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.Blueprint;
using NUnit.Framework;
using Server.Protocol.PacketResponse.Util.Blueprint.Planning;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Game.Blueprint.Planning
{
    public class BlueprintPastePlannerRunOverlapTest
    {
        [Test]
        public void 重複原点の費用を数えず離れた後続BPを置けるTest()
        {
            var context = new BlueprintPastePlannerTestContext(false, 0);
            var block = ForUnitTestModBlockId.BlockId;
            var origins = Origins(Vector3Int.zero, Vector3Int.zero, new Vector3Int(10, 0, 0));
            var plan = BlueprintPastePlanner.Plan(BlueprintPastePlannerTestContext.Create(block), origins, 0,
                context.World, context.Wallet, BlueprintPastePlannerTestContext.BlockCosts(block, 2));

            CollectionAssert.AreEqual(new[] { BlueprintPasteCopyState.Placeable, BlueprintPasteCopyState.AllOverlapped,
                BlueprintPasteCopyState.Placeable }, plan.Copies.Select(copy => copy.State));
            Assert.AreEqual(2, plan.EnumerateCopiesToPlace().Count());
            Assert.IsEmpty(plan.ShortageRequirements);
        }

        [Test]
        public void 部分重複は残るブロックだけを課金して後続BPも置けるTest()
        {
            var context = new BlueprintPastePlannerTestContext(false, 0);
            var block = ForUnitTestModBlockId.BlockId;
            var blueprint = BlueprintPastePlannerTestContext.Create(block, block);
            var origins = Origins(Vector3Int.zero, new Vector3Int(2, 0, 0), new Vector3Int(10, 0, 0));
            var plan = BlueprintPastePlanner.Plan(blueprint, origins, 0, context.World, context.Wallet,
                BlueprintPastePlannerTestContext.BlockCosts(block, 5));

            // 2個、1個、2個を置き、重複分の素材を予約しない
            // Place two, one, and two blocks without reserving materials for the overlap
            Assert.AreEqual(3, plan.EnumerateCopiesToPlace().Count());
            CollectionAssert.AreEqual(new[] { false, true }, plan.Copies[1].Draft.NonOverlapFlags);
            CollectionAssert.AreEqual(new[] { Vector3Int.zero, new Vector3Int(2, 0, 0), new Vector3Int(4, 0, 0),
                new Vector3Int(10, 0, 0), new Vector3Int(12, 0, 0) },
                plan.EnumerateCopiesToPlace().SelectMany(copy => copy.EnumerateElementsToPlace()).Select(element => element.Position));
            Assert.IsEmpty(plan.ShortageRequirements);
        }

        [Test]
        public void 部分重複で片端が置けなくなる後続BPの配線を除外するTest()
        {
            var context = new BlueprintPastePlannerTestContext(true, 0);
            var block = ForUnitTestModBlockId.ElectricPoleId;
            var blueprint = BlueprintPastePlannerTestContext.Create(block, block);
            blueprint.Wires.Add(new BlueprintLineJsonObject(0, 1, BlueprintPastePlannerTestContext.WireGuid));
            var plan = BlueprintPastePlanner.Plan(blueprint, Origins(Vector3Int.zero, new Vector3Int(2, 0, 0)), 0,
                context.World, context.Wallet, new Dictionary<ItemId, int>());

            Assert.AreEqual(2, plan.EnumerateCopiesToPlace().Count());
            Assert.AreEqual(1, plan.Copies[0].Draft.Lines.Count);
            Assert.IsEmpty(plan.Copies[1].Draft.Lines);
            Assert.AreEqual(new Vector3Int(4, 0, 0), plan.Copies[1].EnumerateElementsToPlace().Single().Position);
        }

        [TestCase(0)]
        [TestCase(1)]
        public void 原点が異なる多セルブロックの占有重複を回転後も検出するTest(int rotation)
        {
            var context = new BlueprintPastePlannerTestContext(true, 0);
            var blueprint = BlueprintPastePlannerTestContext.Create(ForUnitTestModBlockId.MultiBlockGeneratorId);
            var longAxis = rotation == 0 ? Vector3Int.right : new Vector3Int(0, 0, 1);
            var plan = BlueprintPastePlanner.Plan(blueprint, Origins(Vector3Int.zero, longAxis, longAxis * 3), rotation,
                context.World, context.Wallet, new Dictionary<ItemId, int>());

            // 3セル幅の箱を1セルずらすと重複、3セルずらすと隣接する
            // Shifting a three-cell footprint by one overlaps; shifting by three only touches its boundary
            CollectionAssert.AreEqual(new[] { BlueprintPasteCopyState.Placeable, BlueprintPasteCopyState.AllOverlapped,
                BlueprintPasteCopyState.Placeable }, plan.Copies.Select(copy => copy.State));
        }

        [Test]
        public void 外接箱の空白部分は後続BPのブロックを妨げないTest()
        {
            var context = new BlueprintPastePlannerTestContext(false, 0);
            var block = ForUnitTestModBlockId.BlockId;
            var blueprint = BlueprintPastePlannerTestContext.Create(block, block);
            var plan = BlueprintPastePlanner.Plan(blueprint, Origins(Vector3Int.zero, Vector3Int.right), 0,
                context.World, context.Wallet, BlueprintPastePlannerTestContext.BlockCosts(block, 4));

            Assert.AreEqual(2, plan.EnumerateCopiesToPlace().Count());
            Assert.IsTrue(plan.Copies[1].Draft.NonOverlapFlags.All(flag => flag));
        }

        [Test]
        public void 地形で拒否された先行BPは同じ原点を予約しないTest()
        {
            var context = new BlueprintPastePlannerTestContext(false, 0);
            var block = ForUnitTestModBlockId.BlockId;
            var origins = new[] { new BlueprintPasteOrigin(Vector3Int.zero, false), new BlueprintPasteOrigin(Vector3Int.zero, true) };
            var plan = BlueprintPastePlanner.Plan(BlueprintPastePlannerTestContext.Create(block), origins, 0,
                context.World, context.Wallet, BlueprintPastePlannerTestContext.BlockCosts(block, 1));

            CollectionAssert.AreEqual(new[] { BlueprintPasteCopyState.GroundNotFound, BlueprintPasteCopyState.Placeable },
                plan.Copies.Select(copy => copy.State));
        }

        [Test]
        public void 素材不足で拒否された先行BPは同じ原点を予約しないTest()
        {
            var context = new BlueprintPastePlannerTestContext(false, 0);
            var blueprint = BlueprintPastePlannerTestContext.Create(ForUnitTestModBlockId.BlockId);
            var plan = BlueprintPastePlanner.Plan(blueprint, Origins(Vector3Int.zero, Vector3Int.zero), 0,
                context.World, context.Wallet, new Dictionary<ItemId, int>());

            Assert.AreEqual(2, plan.CountCopies(BlueprintPasteCopyState.MaterialShortage));
            Assert.IsTrue(plan.Copies[1].Draft.NonOverlapFlags.Single());
        }

        private static BlueprintPasteOrigin[] Origins(params Vector3Int[] positions)
        {
            return positions.Select(position => new BlueprintPasteOrigin(position, true)).ToArray();
        }
    }
}
