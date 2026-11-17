using System.Linq;
using System.Text.RegularExpressions;
using Game.SaveLoad.Migration.Steps.V10ToV11;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.UnitTest.Game.SaveLoad.Migration.V10ToV11
{
    // 旧1マスの床・壁パネルが world とブループリントから外れ、他は残ることを検証
    // Verify old one-cell floor/wall panels leave the world and blueprints while everything else stays
    public class SaveMigrationStepV10ToV11Test
    {
        private const string FloorPanelGuid = "a069613c-bcb5-4b19-a956-1e264a23f81e";
        private const string WallPanelGuid = "de157d57-7b41-49f5-9228-3762fb196e71";
        private const string OtherGuid = "00000000-0000-0000-0000-000000000123";

        [Test]
        public void worldの床と壁パネルだけを撤去するTest()
        {
            var world = new JArray(WorldBlock(1, FloorPanelGuid, 0, 0, 0), WorldBlock(2, OtherGuid, 3, 0, 0), WorldBlock(3, WallPanelGuid, 0, 1, 0));
            LogAssert.Expect(LogType.Warning, new Regex("旧1マスのパネルを撤去しました。 件数=2"));

            var result = new SaveMigrationStepV10ToV11().Migrate(new JObject { ["worldVersion"] = 10, ["world"] = world });

            Assert.IsTrue(result.IsConverted, result.FailureReason);
            CollectionAssert.AreEqual(new[] { 2 }, result.Save["world"].Select(block => block["instanceId"].Value<int>()).ToArray());
        }

        // [0]他 [1]床 [2]他 [3]壁。床・壁に繋がる線は消え、他同士の線は添字を詰めて残る
        // [0]other [1]floor [2]other [3]wall; lines touching panels go, lines between others survive with compacted indices
        [Test]
        public void ブループリントのパネルを外し電線とチェーンの添字を詰めるTest()
        {
            var blueprint = new JObject
            {
                ["name"] = "bp",
                ["blocks"] = new JArray(BlueprintBlock(OtherGuid, 0), BlueprintBlock(FloorPanelGuid, 1), BlueprintBlock(OtherGuid, 2), BlueprintBlock(WallPanelGuid, 3)),
                ["wires"] = new JArray(Line(0, 2), Line(1, 2)),
                ["chains"] = new JArray(Line(3, 0), Line(2, 0)),
            };
            LogAssert.Expect(LogType.Warning, new Regex("ブループリント内の旧1マスの床・壁パネルを外しました"));
            LogAssert.Expect(LogType.Log, new Regex("版10から版11へ変換しました"));

            var result = new SaveMigrationStepV10ToV11().Migrate(new JObject { ["worldVersion"] = 10, ["world"] = new JArray(), ["blueprints"] = new JArray(blueprint) });

            Assert.IsTrue(result.IsConverted, result.FailureReason);
            var migrated = result.Save["blueprints"][0];
            CollectionAssert.AreEqual(new[] { 0, 2 }, migrated["blocks"].Select(block => block["offsetX"].Value<int>()).ToArray());
            Assert.AreEqual(1, migrated["wires"].Count());
            Assert.AreEqual(0, migrated["wires"][0]["blockIndexA"].Value<int>());
            Assert.AreEqual(1, migrated["wires"][0]["blockIndexB"].Value<int>());
            Assert.AreEqual(1, migrated["chains"].Count());
            Assert.AreEqual(1, migrated["chains"][0]["blockIndexA"].Value<int>());
            Assert.AreEqual(0, migrated["chains"][0]["blockIndexB"].Value<int>());
        }

        [Test]
        public void blockGuidが読めなければ拒否してセーブを変えないTest()
        {
            var world = new JArray(WorldBlock(1, FloorPanelGuid, 0, 0, 0), new JObject { ["instanceId"] = 2 });
            LogAssert.Expect(LogType.Warning, new Regex("版10から版11へ変換できません"));

            var result = new SaveMigrationStepV10ToV11().Migrate(new JObject { ["worldVersion"] = 10, ["world"] = world });

            Assert.IsFalse(result.IsConverted);
            Assert.AreEqual(2, world.Count);
        }

        [Test]
        public void 版番号はステップが触らず連鎖へ任せるTest()
        {
            var save = new JObject { ["worldVersion"] = 10, ["world"] = new JArray() };

            var result = new SaveMigrationStepV10ToV11().Migrate(save);

            Assert.IsTrue(result.IsConverted, result.FailureReason);
            Assert.AreEqual(10, result.Save["worldVersion"].Value<int>());
            Assert.AreEqual(10, new SaveMigrationStepV10ToV11().FromVersion);
        }

        private static JObject WorldBlock(int instanceId, string guid, int x, int y, int z)
        {
            return new JObject { ["blockGuid"] = guid, ["instanceId"] = instanceId, ["X"] = x, ["Y"] = y, ["Z"] = z, ["direction"] = 0, ["state"] = null };
        }

        private static JObject BlueprintBlock(string guid, int offsetX)
        {
            return new JObject { ["offsetX"] = offsetX, ["offsetY"] = 0, ["offsetZ"] = 0, ["blockGuid"] = guid, ["direction"] = 0, ["settings"] = new JObject() };
        }

        private static JObject Line(int blockIndexA, int blockIndexB)
        {
            return new JObject { ["blockIndexA"] = blockIndexA, ["blockIndexB"] = blockIndexB, ["connectToolGuid"] = "00000000-0000-0000-0000-0000000000aa" };
        }
    }
}
