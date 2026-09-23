using Game.Block.Interface;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Game.ConnectOverride
{
    public class BeltConnectionRuleTest
    {
        [Test]
        public void DiagramTwentyFiveCasesAcrossDirectionsKindsAndPlacementOrders()
        {
            new MoorestechServerDIContainerGenerator().Create(
                new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var rows = BeltConnectionFixture.Load().DiagramCases;
            Assert.AreEqual(25, rows.Count);
            foreach (var direction in new[]
                     { BlockDirection.North, BlockDirection.East, BlockDirection.South, BlockDirection.West })
            foreach (var sourceFirst in new[] { true, false })
            foreach (var sourceGear in new[] { true, false })
            foreach (var targetGear in new[] { true, false })
            for (var index = 0; index < rows.Count; index++)
                BeltConnectionFixture.Check(rows[index], direction, sourceFirst, sourceGear,
                    targetGear, $"diagram {index} {direction} sourceFirst={sourceFirst} sourceGear={sourceGear} targetGear={targetGear}");
        }

        [Test]
        public void AllTwoHundredFiftySixFourVoxelCases()
        {
            new MoorestechServerDIContainerGenerator().Create(
                new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var rows = BeltConnectionFixture.Load().Cases;
            Assert.AreEqual(256, rows.Count);
            for (var index = 0; index < rows.Count; index++)
                BeltConnectionFixture.Check(rows[index], BlockDirection.North, true,
                    true, true, $"four voxel {index}");
        }
    }
}
