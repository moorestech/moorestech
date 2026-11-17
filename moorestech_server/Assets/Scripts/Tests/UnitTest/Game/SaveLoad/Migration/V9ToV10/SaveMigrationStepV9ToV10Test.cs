using System.Linq;
using System.Text.RegularExpressions;
using Game.SaveLoad.Migration.Steps.V9ToV10;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static Tests.UnitTest.Game.SaveLoad.Migration.V9ToV10.V9SaveJsonFixture;

namespace Tests.UnitTest.Game.SaveLoad.Migration.V9ToV10
{
    // 12向きの水平4向きへの読替と縦置きの寝かせを検証
    // Verify twelve-direction to four-direction renumbering and lay-flat
    public class SaveMigrationStepV9ToV10Test
    {
        private const string MissingDirection = "<missing>";

        private string _cubeGuid;
        private string _tallGuid;

        [SetUp]
        public void SetUp()
        {
            (_cubeGuid, _tallGuid) = LoadTestMasterGuids();
        }

        [Test]
        public void 水平の向きは北東南西の0から3へ読み替わり座標とguidは変わらないTest()
        {
            var world = new JArray(WorldBlock(1, _cubeGuid, 4, 0, 0, 0), WorldBlock(2, _cubeGuid, 5, 2, 0, 0), WorldBlock(3, _cubeGuid, 6, 4, 0, 0), WorldBlock(4, _cubeGuid, 7, 6, 0, 0));

            var save = Migrate(world);

            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3 }, Directions(save));
            CollectionAssert.AreEqual(new[] { 0, 2, 4, 6 }, save["world"].Select(block => block["X"].Value<int>()).ToArray());
            Assert.IsTrue(save["world"].All(block => block["blockGuid"].Value<string>() == _cubeGuid));
        }

        [Test]
        public void 縦の向きは同じ方位の水平へ寝かせるTest()
        {
            var world = new JArray(WorldBlock(1, _cubeGuid, 0, 0, 0, 0), WorldBlock(2, _cubeGuid, 3, 2, 0, 0), WorldBlock(3, _cubeGuid, 9, 4, 0, 0), WorldBlock(4, _cubeGuid, 10, 6, 0, 0));

            var save = Migrate(world);

            CollectionAssert.AreEqual(new[] { 0, 3, 1, 2 }, Directions(save));
            Assert.AreEqual(4, save["world"].Count());
        }

        [Test]
        public void 縦のクリーンルームパネルは同じ方位の壁パネルへ替わり水平のパネルは替わらないTest()
        {
            var world = new JArray(WorldBlock(1, CleanRoomPanelGuid, 3, 0, 0, 0), WorldBlock(2, CleanRoomPanelGuid, 9, 2, 0, 0), WorldBlock(3, CleanRoomPanelGuid, 4, 4, 0, 0));

            var save = Migrate(world);

            CollectionAssert.AreEqual(new[] { CleanRoomWallPanelGuid, CleanRoomWallPanelGuid, CleanRoomPanelGuid }, save["world"].Select(block => block["blockGuid"].Value<string>()).ToArray());
            CollectionAssert.AreEqual(new[] { 3, 1, 0 }, Directions(save));
        }

        // 1×2×3 を UpNorth で原点に置くと、寝かせた占有は x0・y0..1・z0..2
        // A 1x2x3 placed UpNorth at the origin lies flat over x0, y0..1, z0..2
        [Test]
        public void 寝かせた占有が水平のブロックと重なる縦置きは撤去されるTest()
        {
            var world = new JArray(WorldBlock(1, _tallGuid, 0, 0, 0, 0), WorldBlock(2, _cubeGuid, 4, 0, 1, 2));
            LogAssert.Expect(LogType.Warning, new Regex("重なるため撤去"));

            var save = Migrate(world);

            Assert.AreEqual(1, save["world"].Count());
            Assert.AreEqual(2, save["world"][0]["instanceId"].Value<int>());
        }

        // (0,2,0) は縦のときの占有（y0..2）には入るが、寝かせた占有（y0..1）には入らない
        // (0,2,0) lies inside the vertical footprint (y0..2) but outside the laid-flat one (y0..1)
        [Test]
        public void 寝かせた占有が重ならない縦置きは残るTest()
        {
            var world = new JArray(WorldBlock(1, _tallGuid, 0, 0, 0, 0), WorldBlock(2, _cubeGuid, 4, 0, 2, 0), WorldBlock(3, _cubeGuid, 4, 1, 0, 0));

            var save = Migrate(world);

            Assert.AreEqual(3, save["world"].Count());
            Assert.AreEqual(0, save["world"][0]["direction"].Value<int>());
        }

        // 東西に寝かせると x と z が入れ替わり、1×2×3 の DownEast(9)・DownWest(11) は x0..2・y0..1・z0 を占める
        // Laying flat east/west swaps x and z, so a 1x2x3 DownEast(9) or DownWest(11) occupies x0..2, y0..1, z0
        [TestCase(9)]
        [TestCase(11)]
        public void 東西に寝かせた占有はxとzを入れ替えて判定するTest(int legacyDirection)
        {
            var world = new JArray(WorldBlock(1, _tallGuid, legacyDirection, 0, 0, 0), WorldBlock(2, _cubeGuid, 4, 2, 1, 0));
            LogAssert.Expect(LogType.Warning, new Regex("重なるため撤去"));

            var save = Migrate(world);

            Assert.AreEqual(1, save["world"].Count());
            Assert.AreEqual(2, save["world"][0]["instanceId"].Value<int>());
        }

        // 重なる縦置き同士は配列の順に依らず instanceId の小さい方が残る
        // Of two overlapping vertical blocks the smaller instanceId survives regardless of array order
        [Test]
        public void 重なる縦置き同士はinstanceIdの小さい方が残るTest()
        {
            var world = new JArray(WorldBlock(9, _cubeGuid, 8, 0, 0, 0), WorldBlock(5, _cubeGuid, 1, 0, 0, 0));
            LogAssert.Expect(LogType.Warning, new Regex("重なるため撤去"));

            var save = Migrate(world);

            Assert.AreEqual(1, save["world"].Count());
            Assert.AreEqual(5, save["world"][0]["instanceId"].Value<int>());
        }

        [Test]
        public void マスタに無い縦置きは占有に数えず向きだけ寝かせるTest()
        {
            var world = new JArray(WorldBlock(1, _cubeGuid, 4, 0, 0, 0), WorldBlock(2, MissingMasterGuid, 2, 0, 0, 0));

            var save = Migrate(world);

            Assert.AreEqual(2, save["world"].Count());
            CollectionAssert.AreEqual(new[] { 0, 2 }, Directions(save));
        }

        [TestCase("12")]
        [TestCase("-1")]
        [TestCase("\"North\"")]
        [TestCase(MissingDirection)]
        public void 読めない向きは変換を拒否し原本を書き換えないTest(string directionJson)
        {
            var broken = WorldBlock(2, _cubeGuid, 4, 2, 0, 0);
            if (directionJson == MissingDirection) broken.Remove("direction");
            else broken["direction"] = JToken.Parse(directionJson);
            var save = new JObject { ["worldVersion"] = 9, ["world"] = new JArray(WorldBlock(1, _tallGuid, 0, 0, 0, 0), broken) };
            var before = save.DeepClone();
            LogAssert.Expect(LogType.Warning, new Regex("版9から版10へ変換できません"));

            var result = new SaveMigrationStepV9ToV10().Migrate(save);

            Assert.IsFalse(result.IsConverted);
            Assert.IsTrue(JToken.DeepEquals(before, save));
        }

        [Test]
        public void 版番号はステップが触らず連鎖へ任せるTest()
        {
            var save = Migrate(new JArray(WorldBlock(1, _cubeGuid, 0, 0, 0, 0)));

            Assert.AreEqual(9, save["worldVersion"].Value<int>());
            Assert.AreEqual(9, new SaveMigrationStepV9ToV10().FromVersion);
        }

        #region TestHelper

        private static JObject Migrate(JArray world)
        {
            var result = new SaveMigrationStepV9ToV10().Migrate(new JObject { ["worldVersion"] = 9, ["world"] = world });
            Assert.IsTrue(result.IsConverted, result.FailureReason);
            return result.Save;
        }

        private static int[] Directions(JObject save)
        {
            return save["world"].Select(block => block["direction"].Value<int>()).ToArray();
        }

        #endregion
    }
}
