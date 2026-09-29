using System;
using System.Collections.Generic;
using System.Linq;
using Game.Block.Interface;
using Tests.UnitTest.Game.BeltConnection.Fixtures;
using NUnit.Framework;

namespace Tests.UnitTest.Game.BeltConnection
{
    public class BeltEdgePatternTest
    {
        [TestCase(false, BlockDirection.North)]
        [TestCase(false, BlockDirection.East)]
        [TestCase(false, BlockDirection.South)]
        [TestCase(false, BlockDirection.West)]
        [TestCase(true, BlockDirection.North)]
        [TestCase(true, BlockDirection.East)]
        [TestCase(true, BlockDirection.South)]
        [TestCase(true, BlockDirection.West)]
        public void Approved2401Patterns(bool gear, BlockDirection direction)
        {
            var world = new BeltEdgeTestWorld(gear, direction);
            var cases = ApprovedBeltPatterns.All().ToArray();
            Assert.AreEqual(2401, cases.Length);
            foreach (var row in cases)
            {
                var id = row.Id;
                var expected = row.Connections.Select(e => e.ToString()).ToArray();
                Check(id, expected);
            }

            #region Internal
            void Check(string id, IEnumerable<string> expected)
            {
                for (var i = 0; i < 4; i++)
                    if (id[i] != '0') world.Place(BeltEdgeTestWorld.Slots[i], id[i] - '0');
                world.AssertEdges(expected, id);
                world.Clear();
            }
            #endregion
        }

        [Test]
        public void MirrorAndNonTouchingEmptyEquivalence()
        {
            var world = new BeltEdgeTestWorld(false, BlockDirection.North);
            var reverse = new[] { 0, 4, 5, 6, 1, 2, 3 };
            var swapped = new[] { 1, 0, 3, 2 };
            foreach (var row in ApprovedBeltPatterns.All())
            {
                var id = row.Id;
                // 固定された期待結果を鏡映し、実装の判定式は複製しない
                // Mirror the fixed expectation without reproducing the implementation's rules
                for (var i = 0; i < 4; i++)
                {
                    var code = reverse[id[swapped[i]] - '0'];
                    if (code != 0) world.Place(BeltEdgeTestWorld.Slots[i], code);
                }
                var expected = row.Connections.Select(e => Swap(e.Source.ToString()) + ">" + Swap(e.Target.ToString())).ToArray();
                world.AssertEdges(expected, id + " mirrored");
                world.Clear();

                for (var i = 0; i < 4; i++)
                    if (row.TouchingSlots.Contains((BeltTestSlot)i)) world.Place(BeltEdgeTestWorld.Slots[i], id[i] - '0');
                expected = row.Connections.Select(e => e.ToString()).ToArray();
                world.AssertEdges(expected, id + " non-touching cleared");
                world.Clear();
            }

            #region Internal
            string Swap(string slot) => slot.Substring(0, 1) + (slot[1] == 'L' ? "R" : "L");
            #endregion
        }

        [TestCase(-1)]
        [TestCase(0)]
        [TestCase(7)]
        [TestCase(int.MaxValue)]
        public void PlacementRejectsUndefinedOccupiedState(int code)
        {
            var world = new BeltEdgeTestWorld(false, BlockDirection.North);
            Assert.Throws<ArgumentOutOfRangeException>(() => world.Place("UL", code));
            Assert.AreEqual(0, world.World.BlockMasterDictionary.Count);
        }

        [Test]
        public void Original256And25DiagramExpectations()
        {
            var world = new BeltEdgeTestWorld(false, BlockDirection.North);
            var cases = OriginalBeltPatterns.Cases().ToArray();
            var diagrams = OriginalBeltPatterns.Diagrams().ToArray();
            Assert.AreEqual(256, cases.Length);
            Assert.AreEqual(25, diagrams.Length);
            foreach (var row in cases.Concat(diagrams))
            {
                for (var i = 0; i < 4; i++)
                    if (row.Id[i] != '0') world.Place(BeltEdgeTestWorld.Slots[i], row.Id[i] - '0');
                world.AssertEdges(row.Connections.Select(e => e.ToString()), row.Id);
                world.Clear();
            }
        }
    }
}
