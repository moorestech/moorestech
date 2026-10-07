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
        public void ApprovedFixturesMatchMirrorAndNonTouchingEmptyExpectations()
        {
            var casesById = ApprovedBeltPatterns.All().ToDictionary(row => row.Id);
            var reverse = new[] { 0, 4, 5, 6, 1, 2, 3 };
            var swapped = new[] { 1, 0, 3, 2 };
            foreach (var row in casesById.Values)
            {
                var id = row.Id;
                // 固定された期待結果を鏡映し、実装の判定式は複製しない
                // Mirror the fixed expectation without reproducing the implementation's rules
                var mirroredId = new string(swapped.Select(slot => (char)('0' + reverse[id[slot] - '0'])).ToArray());
                var expected = row.Connections.Select(e => Swap(e.Source.ToString()) + ">" + Swap(e.Target.ToString())).ToArray();
                CollectionAssert.AreEquivalent(expected, casesById[mirroredId].Connections.Select(e => e.ToString()), id + " mirrored");

                // 非接触スロットを空にしても固定期待値は変わらない
                // Clearing non-touching slots preserves the fixed expectation
                var touchingId = id.ToCharArray();
                for (var i = 0; i < 4; i++)
                    if (!row.TouchingSlots.Contains((BeltTestSlot)i)) touchingId[i] = '0';
                expected = row.Connections.Select(e => e.ToString()).ToArray();
                CollectionAssert.AreEquivalent(expected, casesById[new string(touchingId)].Connections.Select(e => e.ToString()), id + " non-touching cleared");
            }

            #region Internal
            string Swap(string slot) => slot.Substring(0, 1) + (slot[1] == 'L' ? "R" : "L");
            #endregion
        }

        [Test]
        public void ApprovedFixturesMatchOriginal256And25DiagramExpectations()
        {
            var casesById = ApprovedBeltPatterns.All().ToDictionary(row => row.Id);
            var cases = OriginalBeltPatterns.Cases().ToArray();
            var diagrams = OriginalBeltPatterns.Diagrams().ToArray();
            Assert.AreEqual(256, cases.Length);
            Assert.AreEqual(25, diagrams.Length);
            // 元の独立期待値を承認済み表と照合する
            // Compare the original independent expectations with the approved table
            foreach (var row in cases.Concat(diagrams))
            {
                CollectionAssert.AreEquivalent(row.Connections.Select(e => e.ToString()),
                    casesById[row.Id].Connections.Select(e => e.ToString()), row.Id);
            }
        }
    }
}
