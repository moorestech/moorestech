using System.Collections.Generic;
using System.Linq;
using Core.Master;
using NUnit.Framework;
using Server.Protocol.PacketResponse.Util.Blueprint.Planning;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Game.Blueprint.Planning
{
    public class BlueprintPasteCopyBoundsTest
    {
        [Test]
        public void 非重複の100ブロックBPを64原点へ配置できるTest()
        {
            var context = new BlueprintPastePlannerTestContext(true, 0);
            var blueprint = BlueprintPastePlannerTestContext.Create(
                Enumerable.Repeat(ForUnitTestModBlockId.BlockId, 100).ToArray());
            var origins = Enumerable.Range(0, 64)
                .Select(index => new BlueprintPasteOrigin(new Vector3Int(index * 200, 0, 0), true)).ToArray();

            var plan = BlueprintPastePlanner.Plan(blueprint, origins, 0, context.World, context.Wallet,
                new Dictionary<ItemId, int>());

            Assert.AreEqual(64, plan.EnumerateCopiesToPlace().Count());
            Assert.AreEqual(6400, plan.EnumerateCopiesToPlace().Sum(copy => copy.EnumerateElementsToPlace().Count()));
            Assert.IsTrue(plan.Copies.All(copy => copy.Draft.NonOverlapFlags.All(flag => flag)));
        }

        [TestCase(0)]
        [TestCase(1)]
        public void 外接箱が交差しても空白にあるブロックだけは予約できるTest(int rotation)
        {
            var context = new BlueprintPastePlannerTestContext(true, 0);
            var blueprint = BlueprintPastePlannerTestContext.Create(
                Enumerable.Repeat(ForUnitTestModBlockId.BlockId, 100).ToArray());
            var axis = rotation == 0 ? Vector3Int.right : new Vector3Int(0, 0, 1);
            var origins = new[] { new BlueprintPasteOrigin(Vector3Int.zero, true),
                new BlueprintPasteOrigin(axis, true), new BlueprintPasteOrigin(axis * 2, true) };

            var plan = BlueprintPastePlanner.Plan(blueprint, origins, rotation, context.World, context.Wallet,
                new Dictionary<ItemId, int>());

            // 奇数セルは空白、三個目は末尾だけが空く
            // Odd cells are gaps; only the last cell of the third copy is free
            Assert.AreEqual(3, plan.EnumerateCopiesToPlace().Count());
            Assert.AreEqual(100, plan.Copies[1].EnumerateElementsToPlace().Count());
            Assert.AreEqual(axis * 200, plan.Copies[2].EnumerateElementsToPlace().Single().Position);
        }
    }
}
