using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Blueprint;
using NUnit.Framework;
using Server.Protocol.PacketResponse.Util.Blueprint.Planning;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Game.Blueprint.Planning
{
    public class BlueprintPastePlannerBoundaryTest
    {
        [Test]
        public void 最大整数の占有セル列挙は一件で終了するTest()
        {
            var origin = new Vector3Int(int.MaxValue, int.MaxValue, int.MaxValue);
            var position = new BlockPositionInfo(origin, BlockDirection.North, Vector3Int.one);
            CollectionAssert.AreEqual(new[] { origin }, position.EnumeratePositions().Take(2));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void 最大セルを超える多セルBPは座標不正になるTest(int rotation)
        {
            var context = new BlueprintPastePlannerTestContext(true, 0);
            var blueprint = BlueprintPastePlannerTestContext.Create(ForUnitTestModBlockId.MultiBlockGeneratorId);
            var origins = new[] { new BlueprintPasteOrigin(new Vector3Int(int.MaxValue, int.MaxValue, int.MaxValue), true) };
            var plan = BlueprintPastePlanner.Plan(blueprint, origins, rotation, context.World, context.Wallet,
                new Dictionary<ItemId, int>());
            Assert.AreEqual(BlueprintPasteCopyState.InvalidCoordinates, plan.Copies[0].State);
            Assert.IsEmpty(plan.EnumerateCopiesToPlace());
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void 最小整数の保存座標も回転して正規化できるTest(int rotation)
        {
            var context = new BlueprintPastePlannerTestContext(true, 0);
            var blueprint = BlueprintPastePlannerTestContext.Create(ForUnitTestModBlockId.BlockId, ForUnitTestModBlockId.BlockId);
            blueprint.Blocks[0].OffsetX = int.MinValue;
            blueprint.Blocks[1].OffsetX = int.MinValue + 2;
            Assert.IsTrue(BlueprintPasteCalculator.TryCalculatePlacements(blueprint, Vector3Int.zero, rotation, out var elements));
            Assert.AreEqual(2f, Vector3Int.Distance(elements[0].Position, elements[1].Position));
            Assert.AreEqual(Vector3Int.zero, Vector3Int.Min(elements[0].Position, elements[1].Position));
        }

        [Test]
        public void 欠損端点の未解放線種は残るブロックを拒否しないTest()
        {
            var context = new BlueprintPastePlannerTestContext(true, 0);
            var blueprint = BlueprintPastePlannerTestContext.Create(ForUnitTestModBlockId.ElectricPoleId);
            blueprint.Blocks.Add(new BlueprintBlockJsonObject(Vector3Int.right, Guid.NewGuid().ToString(),
                (int)BlockDirection.North, new Dictionary<string, string>()));
            blueprint.Wires.Add(new BlueprintLineJsonObject(0, 1, BlueprintPastePlannerTestContext.WireGuid));
            context.World.Locked.Add(BlueprintPastePlannerTestContext.WireGuid);
            var plan = context.Plan(blueprint, 1, new Dictionary<ItemId, int>());
            Assert.AreEqual(BlueprintPasteCopyState.Placeable, plan.Copies[0].State);
            Assert.AreEqual(1, plan.Copies[0].Draft.MissingEndpointLineCount);
        }

        [Test]
        public void 不足後に安くなるコピーも設置しないTest()
        {
            var context = new BlueprintPastePlannerTestContext(false, 0);
            var block = ForUnitTestModBlockId.BlockId;
            var blueprint = BlueprintPastePlannerTestContext.Create(block, block);
            context.World.Overlaps.Add(new Vector3Int(20, 0, 0));
            var plan = context.Plan(blueprint, 3, BlueprintPastePlannerTestContext.BlockCosts(block, 3));

            // 二個目は二セル必要、三個目は残る一セルで払える
            // The second copy needs two cells; the third would fit the remaining one
            CollectionAssert.AreEqual(new[] { BlueprintPasteCopyState.Placeable, BlueprintPasteCopyState.MaterialShortage,
                BlueprintPasteCopyState.MaterialShortage }, plan.Copies.Select(copy => copy.State));
            CollectionAssert.AreEqual(new[] { false, true }, plan.Copies[2].Draft.NonOverlapFlags);
        }
    }
}
