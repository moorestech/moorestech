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
    public class BlueprintPastePlannerTest
    {
        [Test]
        public void 素材が1BP分だけあれば列の1個目だけ置けるTest()
        {
            var context = new BlueprintPastePlannerTestContext(false, 0);
            var block = ForUnitTestModBlockId.BlockId;
            var held = BlueprintPastePlannerTestContext.BlockCosts(block, 2);
            Assert.IsNotEmpty(held);
            var result = context.Plan(BlueprintPastePlannerTestContext.Create(block, block), 3, held);

            // 不足境界以降はBP全体を拒否する
            // Reject entire copies from the first shortage onward
            CollectionAssert.AreEqual(new[] { BlueprintPasteCopyState.Placeable, BlueprintPasteCopyState.MaterialShortage,
                BlueprintPasteCopyState.MaterialShortage }, result.Copies.Select(copy => copy.State));
            Assert.AreEqual(1, result.EnumerateCopiesToPlace().Count());
            Assert.AreEqual(2, result.CountCopies(BlueprintPasteCopyState.MaterialShortage));
            Assert.IsTrue(result.ShortageRequirements.Any(row => row.held < row.required));
        }

        [Test]
        public void 一部重なるBPは重ならない分の素材だけ要求するTest()
        {
            var context = new BlueprintPastePlannerTestContext(false, 0);
            context.World.Overlaps.Add(Vector3Int.zero);
            var block = ForUnitTestModBlockId.BlockId;
            var result = context.Plan(BlueprintPastePlannerTestContext.Create(block, block), 1,
                BlueprintPastePlannerTestContext.BlockCosts(block, 1));

            Assert.IsTrue(result.Copies[0].IsPlaced);
            CollectionAssert.AreEqual(new[] { false, true }, result.Copies[0].Draft.NonOverlapFlags);
            Assert.AreEqual(1, result.Copies[0].EnumerateElementsToPlace().Count());
            Assert.AreEqual(new Vector3Int(2, 0, 0), result.Copies[0].EnumerateElementsToPlace().Single().Position);
        }

        [Test]
        public void 全ブロック重なりはAllOverlappedTest()
        {
            var context = new BlueprintPastePlannerTestContext(false, 0);
            context.World.Overlaps.Add(Vector3Int.zero);
            var result = context.Plan(BlueprintPastePlannerTestContext.Create(ForUnitTestModBlockId.BlockId), 1,
                new Dictionary<ItemId, int>());
            Assert.AreEqual(BlueprintPasteCopyState.AllOverlapped, result.Copies[0].State);
            Assert.IsEmpty(result.EnumerateCopiesToPlace());
            Assert.IsEmpty(result.ShortageRequirements);
        }

        [Test]
        public void 地形が取れない原点は素材計算に入らないTest()
        {
            var context = new BlueprintPastePlannerTestContext(false, 0);
            var block = ForUnitTestModBlockId.BlockId;
            var origins = new[] { new BlueprintPasteOrigin(Vector3Int.zero, false),
                new BlueprintPasteOrigin(new Vector3Int(10, 0, 0), true) };
            var result = BlueprintPastePlanner.Plan(BlueprintPastePlannerTestContext.Create(block), origins, 0,
                context.World, context.Wallet, BlueprintPastePlannerTestContext.BlockCosts(block, 1));

            CollectionAssert.AreEqual(new[] { BlueprintPasteCopyState.GroundNotFound, BlueprintPasteCopyState.Placeable },
                result.Copies.Select(copy => copy.State));
            Assert.IsEmpty(result.ShortageRequirements);
        }

        [Test]
        public void 未解放ブロックを含むBPはNotUnlockedTest()
        {
            var context = new BlueprintPastePlannerTestContext(true, 0);
            var block = ForUnitTestModBlockId.BlockId;
            context.World.Locked.Add(MasterHolder.BlockMaster.GetBlockMaster(block).BlockGuid);
            var result = context.Plan(BlueprintPastePlannerTestContext.Create(block), 1, new Dictionary<ItemId, int>());
            Assert.AreEqual(BlueprintPasteCopyState.NotUnlocked, result.Copies[0].State);
        }

        [Test]
        public void 重なりで省略される未解放ブロックもBPを拒否するTest()
        {
            var context = new BlueprintPastePlannerTestContext(true, 0);
            var locked = ForUnitTestModBlockId.BlockId;
            context.World.Locked.Add(MasterHolder.BlockMaster.GetBlockMaster(locked).BlockGuid);
            context.World.Overlaps.Add(Vector3Int.zero);
            var blueprint = BlueprintPastePlannerTestContext.Create(locked, ForUnitTestModBlockId.ChestId);
            var result = context.Plan(blueprint, 1, new Dictionary<ItemId, int>());
            Assert.AreEqual(BlueprintPasteCopyState.NotUnlocked, result.Copies[0].State);
        }

        [Test]
        public void 生成後のCopyPlanは状態を変える口を持たないTest()
        {
            // 状態を変えるpublic APIを追加させない
            // Keep state mutation out of the public result API
            var type = typeof(BlueprintPasteCopyPlan);
            Assert.IsFalse(type.GetProperties().Any(property => property.GetSetMethod() != null));
            Assert.IsFalse(type.GetMethods().Any(method => method.Name.StartsWith("Set")));
        }
    }
}
