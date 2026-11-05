using System.Linq;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route;
using NUnit.Framework;

namespace Client.Tests.PlaceSystem.BeltRoute
{
    // Cキーの並びは6通りを一巡し、隣り合う並びでは必ず水平の順（XとZ）が入れ替わる
    // The C-key order cycles through all six permutations, and adjacent entries always swap the horizontal order (X and Z)
    public class BeltRouteStepPriorityTest
    {
        [Test]
        public void SixPermutationsAreAllDistinct()
        {
            var ranks = Enumerable.Range(0, BeltRouteStepPriority.PermutationCount)
                .Select(index => new BeltRouteStepPriority(index))
                .Select(priority => (priority.Rank(BeltRouteStepAxis.X), priority.Rank(BeltRouteStepAxis.Y), priority.Rank(BeltRouteStepAxis.Z)))
                .ToList();

            Assert.AreEqual(BeltRouteStepPriority.PermutationCount, ranks.Distinct().Count());
            Assert.AreEqual(BeltRouteStepPriority.Default.Index, BeltRouteStepPriority.Default.Next().Next().Next().Next().Next().Next().Index);
        }

        [Test]
        public void AdjacentPermutationsSwapHorizontalOrder()
        {
            // 平地では坂を使わないので、C一回で形が変わるにはXとZの前後が入れ替わる必要がある
            // Flat ground never uses slopes, so one C press changes the shape only when X and Z swap places
            var priority = BeltRouteStepPriority.Default;
            for (var i = 0; i < BeltRouteStepPriority.PermutationCount; i++)
            {
                var next = priority.Next();
                var xFirst = priority.Rank(BeltRouteStepAxis.X) < priority.Rank(BeltRouteStepAxis.Z);
                var nextXFirst = next.Rank(BeltRouteStepAxis.X) < next.Rank(BeltRouteStepAxis.Z);
                Assert.AreNotEqual(xFirst, nextXFirst, $"permutation {priority.Index} -> {next.Index} keeps the horizontal order");
                priority = next;
            }
        }
    }
}
