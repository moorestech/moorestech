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
    public class BlueprintPastePlannerWalletTest
    {
        [Test]
        public void 財布の残数で賄えるBPは素材行を出さないTest()
        {
            var context = new BlueprintPastePlannerTestContext(false, 3);
            var block = ForUnitTestModBlockId.GearBeltConveyor;
            var blueprint = BlueprintPastePlannerTestContext.Create(block, block, block);
            var required = BlueprintPasteCostCalculator.CalcRequiredItems(
                new[] { BlueprintPasteCopyBuilder.BuildUnobstructed(blueprint) }, context.Wallet, false);

            Assert.IsEmpty(required);
            Assert.IsTrue(context.Plan(blueprint, 1, new Dictionary<ItemId, int>()).Copies[0].IsPlaced);
        }

        [Test]
        public void 複数BPが同じ財布の残数を二重使用しないTest()
        {
            var context = new BlueprintPastePlannerTestContext(false, 3);
            var block = ForUnitTestModBlockId.GearBeltConveyor;
            var result = context.Plan(BlueprintPastePlannerTestContext.Create(block, block), 3,
                new Dictionary<ItemId, int>());

            CollectionAssert.AreEqual(new[] { BlueprintPasteCopyState.Placeable, BlueprintPasteCopyState.MaterialShortage,
                BlueprintPasteCopyState.MaterialShortage }, result.Copies.Select(copy => copy.State));
        }

        [Test]
        public void 坂と直線は同じ財布の残数を共有するTest()
        {
            var context = new BlueprintPastePlannerTestContext(false, 1);
            var blueprint = BlueprintPastePlannerTestContext.Create(ForUnitTestModBlockId.GearBeltConveyor,
                ForUnitTestModBlockId.TestGearBeltConveyorUp);
            var result = context.Plan(blueprint, 1, new Dictionary<ItemId, int>());
            Assert.AreEqual(BlueprintPasteCopyState.MaterialShortage, result.Copies[0].State);
        }

        [Test]
        public void 坂と直線の補充素材は一セットにまとまるTest()
        {
            var context = new BlueprintPastePlannerTestContext(false, 0);
            var block = ForUnitTestModBlockId.GearBeltConveyor;
            var blueprint = BlueprintPastePlannerTestContext.Create(block, ForUnitTestModBlockId.TestGearBeltConveyorUp);
            var held = BlueprintPastePlannerTestContext.BlockCosts(block, 1);
            var result = context.Plan(blueprint, 1, held);
            Assert.IsTrue(result.Copies[0].IsPlaced);
        }
    }
}
