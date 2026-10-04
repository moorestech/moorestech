using System;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.TestTools;
using Game.SaveLoad.Migration.Steps;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Tests.UnitTest.Game.SaveLoad.ConnectToolGuidMigration
{
    public class SaveMigrationStepV3ToV4Test
    {
        private const string WireSaveKey = "ElectricWireConnectorComponent";
        private const string ChainSaveKey = "GearChainPoleComponent";
        private const string WireItem = "00000000-0000-0000-1234-000000000001";

        [Test]
        public void 電線とチェーンへ線種別ごとの固定の種類が書き込まれるTest()
        {
            var save = Save(Block(WireSaveKey, Connection(Materials(WireItem))), Block(ChainSaveKey, Connection(Materials(WireItem))));

            // 種類だけを補い既存の接続データを保持する
            // Fill only the tool while preserving existing connection data
            var result = new SaveMigrationStepV3ToV4().Migrate(save);

            Assert.IsTrue(result.IsConverted, result.FailureReason);
            Assert.AreEqual(SaveMigrationStepV3ToV4.ElectricWireConnectToolGuid, ToolOf(result.Save, 0, WireSaveKey));
            Assert.AreEqual(SaveMigrationStepV3ToV4.GearChainConnectToolGuid, ToolOf(result.Save, 1, ChainSaveKey));

            // 補填済みの再実行で素材や既存の値を変えない
            // Repeating the fill must preserve materials and existing values
            var once = result.Save.DeepClone();
            Assert.IsTrue(new SaveMigrationStepV3ToV4().Migrate(result.Save).IsConverted);
            Assert.IsTrue(JToken.DeepEquals(once, result.Save));
            Assert.AreEqual(3, result.Save["worldVersion"].Value<int>());
            Assert.IsTrue(JToken.DeepEquals(Materials(WireItem), result.Save["world"][0]["state"][WireSaveKey]["connections"][0]["materials"]));
        }

        [Test]
        public void 素材が空の接続も移行されるTest()
        {
            var save = Save(Block(WireSaveKey, Connection(new JArray())), Block(ChainSaveKey, Connection(new JArray())));

            var result = new SaveMigrationStepV3ToV4().Migrate(save);

            Assert.IsTrue(result.IsConverted, result.FailureReason);
            Assert.AreEqual(SaveMigrationStepV3ToV4.ElectricWireConnectToolGuid, ToolOf(result.Save, 0, WireSaveKey));
            Assert.AreEqual(SaveMigrationStepV3ToV4.GearChainConnectToolGuid, ToolOf(result.Save, 1, ChainSaveKey));
        }

        [Test]
        public void 未知の素材の接続も移行されるTest()
        {
            var unknown = Materials("ffffffff-ffff-ffff-ffff-ffffffffffff");
            var save = Save(Block(WireSaveKey, Connection(unknown)), Block(ChainSaveKey, Connection((JArray)unknown.DeepClone())));

            var result = new SaveMigrationStepV3ToV4().Migrate(save);

            Assert.IsTrue(result.IsConverted, result.FailureReason);
            Assert.AreEqual(SaveMigrationStepV3ToV4.ElectricWireConnectToolGuid, ToolOf(result.Save, 0, WireSaveKey));
            Assert.AreEqual(SaveMigrationStepV3ToV4.GearChainConnectToolGuid, ToolOf(result.Save, 1, ChainSaveKey));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void 既に種類がある接続は上書きされないTest(bool guidToken)
        {
            var existing = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
            var connection = Connection(Materials(WireItem));
            connection["connectToolGuid"] = guidToken ? new JValue(existing) : new JValue(existing.ToString());
            var save = Save(Block(WireSaveKey, connection));

            var result = new SaveMigrationStepV3ToV4().Migrate(save);

            Assert.IsTrue(result.IsConverted, result.FailureReason);
            var once = result.Save.DeepClone();
            Assert.IsTrue(new SaveMigrationStepV3ToV4().Migrate(result.Save).IsConverted);
            Assert.IsTrue(JToken.DeepEquals(once, result.Save));
            Assert.AreEqual(existing, ToolOf(result.Save, 0, WireSaveKey));
            Assert.AreEqual(guidToken ? JTokenType.Guid : JTokenType.String, result.Save["world"][0]["state"][WireSaveKey]["connections"][0]["connectToolGuid"].Type);
        }

        [Test]
        public void 接続を持たないブロックは素通しされるTest()
        {
            var save = Save(JObject.Parse("{\"blockGuid\":\"x\",\"state\":{\"ChestComponent\":{}}}"), JObject.Parse("{\"blockGuid\":\"y\",\"state\":{}}"), JObject.Parse("{\"blockGuid\":\"z\"}"), JObject.Parse("{\"state\":null}"));

            var result = new SaveMigrationStepV3ToV4().Migrate(save);

            Assert.IsTrue(result.IsConverted, result.FailureReason);
        }

        [Test]
        public void connectionsが配列でない壊れた形はFailedで返るTest()
        {
            var save = Save(new JObject { ["blockGuid"] = "x", ["state"] = new JObject { [WireSaveKey] = new JObject { ["connections"] = "broken" } } });

            var result = new SaveMigrationStepV3ToV4().Migrate(save);

            Assert.IsFalse(result.IsConverted);
            StringAssert.Contains(WireSaveKey, result.FailureReason);
        }

        [Test]
        public void worldが配列でないセーブはFailedで返るTest()
        {
            var result = new SaveMigrationStepV3ToV4().Migrate(JObject.Parse("{\"world\":{}}"));

            Assert.IsFalse(result.IsConverted);
        }

        [Test]
        public void FromVersionは3であるTest()
        {
            Assert.AreEqual(3, new SaveMigrationStepV3ToV4().FromVersion);
        }

        // 辿れない外部データは版を上げず拒否する
        // Refuse unwalkable external data without advancing its version
        [TestCase("{\"world\":[null]}")]
        [TestCase("{\"world\":[{\"state\":[]}]}")]
        [TestCase("{\"world\":[{\"state\":{\"ElectricWireConnectorComponent\":null}}]}")]
        [TestCase("{\"world\":[{\"state\":{\"GearChainPoleComponent\":[]}}]}")]
        [TestCase("{\"world\":[{\"state\":{\"ElectricWireConnectorComponent\":{}}}]}")]
        [TestCase("{\"world\":[{\"state\":{\"GearChainPoleComponent\":{\"connections\":[null]}}}]}")]
        public void 辿れない構造は理由つきで拒否するTest(string json)
        {
            var save = JObject.Parse(json);
            save["worldVersion"] = 3;
            var result = new SaveMigrationStepV3ToV4().Migrate(save);

            Assert.IsFalse(result.IsConverted);
            Assert.IsNotEmpty(result.FailureReason);
            Assert.AreEqual(3, save["worldVersion"].Value<int>());
        }

        // 不正な既存キーを新版へ通さず、値を消さずに拒否する
        // Reject an unusable existing key without passing it to the new version or erasing it
        [TestCase(WireSaveKey, "null")]
        [TestCase(ChainSaveKey, "null")]
        [TestCase(WireSaveKey, "\"not-a-guid\"")]
        [TestCase(ChainSaveKey, "\"not-a-guid\"")]
        [TestCase(WireSaveKey, "123")]
        [TestCase(ChainSaveKey, "{}")]
        public void 不正な既存種類はログと理由を残して拒否するTest(string saveKey, string toolJson)
        {
            var connection = Connection(new JArray());
            connection["connectToolGuid"] = JToken.Parse(toolJson);
            var save = Save(Block(saveKey, connection));
            var original = save.DeepClone();
            LogAssert.Expect(LogType.Warning, new Regex("版3から版4へ変換できません:.*connectToolGuid"));

            var result = new SaveMigrationStepV3ToV4().Migrate(save);

            Assert.IsFalse(result.IsConverted);
            StringAssert.Contains(saveKey, result.FailureReason);
            StringAssert.Contains("connectToolGuid", result.FailureReason);
            Assert.IsTrue(JToken.DeepEquals(original, save));
        }

        private static JArray Materials(string itemGuid)
        {
            return JArray.Parse($"[{{\"itemGuid\":\"{itemGuid}\",\"count\":3}}]");
        }

        private static JObject Connection(JArray materials)
        {
            return new JObject { ["targetBlockInstanceId"] = 2, ["materials"] = materials };
        }

        private static JObject Block(string saveKey, JObject connection)
        {
            return new JObject { ["blockGuid"] = "x", ["state"] = new JObject { [saveKey] = new JObject { ["connections"] = new JArray(connection) } } };
        }

        private static JObject Save(params JObject[] blocks)
        {
            return new JObject { ["worldVersion"] = 3, ["world"] = new JArray(blocks) };
        }

        private static Guid ToolOf(JObject save, int blockIndex, string saveKey)
        {
            // 文字列とGuid型の両トークンを型どおりに読む
            // Read both string and GUID tokens through GUID conversion
            return save["world"][blockIndex]["state"][saveKey]["connections"][0]["connectToolGuid"].ToObject<Guid>();
        }
    }
}
