using System.Collections.Generic;
using System.Linq;
using Game.Block.Interface;
using Newtonsoft.Json.Linq;
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
            var cases = (JArray)BeltEdgeTestWorld.Read("cases.json");
            Assert.AreEqual(2401, cases.Count);
            foreach (var row in cases)
            {
                var id = (string)row["id"];
                var expected = row["edges"].Select(e => (string)e[0] + ">" + (string)e[1]).ToArray();
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
            foreach (var row in BeltEdgeTestWorld.Read("cases.json"))
            {
                var id = (string)row["id"];
                // 固定された期待結果を鏡映し、実装の判定式は複製しない
                // Mirror the fixed expectation without reproducing the implementation's rules
                for (var i = 0; i < 4; i++)
                {
                    var code = reverse[id[swapped[i]] - '0'];
                    if (code != 0) world.Place(BeltEdgeTestWorld.Slots[i], code);
                }
                var expected = row["edges"].Select(e => Swap((string)e[0]) + ">" + Swap((string)e[1])).ToArray();
                world.AssertEdges(expected, id + " mirrored");
                world.Clear();

                for (var i = 0; i < 4; i++)
                    if (row["touching"].Values<string>().Contains(BeltEdgeTestWorld.Slots[i])) world.Place(BeltEdgeTestWorld.Slots[i], id[i] - '0');
                expected = row["edges"].Select(e => (string)e[0] + ">" + (string)e[1]).ToArray();
                world.AssertEdges(expected, id + " non-touching cleared");
                world.Clear();
            }

            #region Internal
            string Swap(string slot) => slot.Substring(0, 1) + (slot[1] == 'L' ? "R" : "L");
            #endregion
        }

        [Test]
        public void Original256And25DiagramExpectations()
        {
            var world = new BeltEdgeTestWorld(false, BlockDirection.North);
            var oracle = BeltEdgeTestWorld.Read("original_256_expected.json");
            var kinds = new Dictionary<string, int> { ["Empty"] = 0, ["Flat"] = 1, ["Up"] = 2, ["Down"] = 3 };
            foreach (var row in oracle["cases"].Concat(oracle["diagramCases"]))
            {
                var names = new[] { "upperSource", "upperTarget", "lowerSource", "lowerTarget" };
                for (var i = 0; i < 4; i++)
                {
                    var code = kinds[(string)row[names[i]]];
                    if (code != 0) world.Place(BeltEdgeTestWorld.Slots[i], code);
                }
                var expected = new List<string>();
                if ((string)row["source"] != null)
                    expected.Add(((string)row["source"] == "Upper" ? "UL" : "LL") + ">" + ((string)row["target"] == "Upper" ? "UR" : "LR"));
                world.AssertEdges(expected, row.ToString());
                world.Clear();
            }
        }
    }
}
