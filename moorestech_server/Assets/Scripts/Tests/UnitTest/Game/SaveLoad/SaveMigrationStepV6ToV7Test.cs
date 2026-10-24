using Game.SaveLoad.Migration.Steps;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Tests.UnitTest.Game.SaveLoad
{
    public class SaveMigrationStepV6ToV7Test
    {
        private const string VanillaKey = "Game.Block.Blocks.Machine.VanillaMachineSaveComponent";
        private const string CleanRoomKey = "Game.Block.Blocks.CleanRoom.Machine.CleanRoomMachineProcessorComponent";

        [Test]
        public void 通常機械とクリーンルーム機械に継承レベルnullが入り他のキーは残るTest()
        {
            var vanilla = JObject.Parse("{\"inputSlot\":[],\"processor\":{\"state\":1,\"remainingSeconds\":0.5,\"recipeGuid\":\"r\"}}");
            var cleanRoom = JObject.Parse("{\"cycleCount\":3,\"state\":0}");
            var save = Save(
                new JObject { ["state"] = new JObject { [VanillaKey] = vanilla } },
                new JObject { ["state"] = new JObject { [CleanRoomKey] = cleanRoom } });

            var result = new SaveMigrationStepV6ToV7().Migrate(save);

            Assert.IsTrue(result.IsConverted, result.FailureReason);
            var migratedVanilla = (JObject)result.Save["world"][0]["state"][VanillaKey];
            Assert.AreEqual(JTokenType.Null, migratedVanilla["processor"]["inheritedLevel"].Type);
            Assert.AreEqual(1, migratedVanilla["processor"]["state"].Value<int>());
            Assert.AreEqual("r", migratedVanilla["processor"]["recipeGuid"].Value<string>());
            var migratedCleanRoom = (JObject)result.Save["world"][1]["state"][CleanRoomKey];
            Assert.AreEqual(JTokenType.Null, migratedCleanRoom["inheritedLevel"].Type);
            Assert.AreEqual(3, migratedCleanRoom["cycleCount"].Value<int>());
        }

        [Test]
        public void 既に継承レベルを持つものと機械以外は素通しされるTest()
        {
            var save = Save(
                JObject.Parse($"{{\"state\":{{\"{VanillaKey}\":{{\"processor\":{{\"inheritedLevel\":3}}}}}}}}"),
                JObject.Parse("{\"state\":{\"ChestComponent\":{\"items\":[]}}}"),
                JObject.Parse("{\"blockGuid\":\"z\"}"),
                JObject.Parse("{\"state\":null}"));

            var result = new SaveMigrationStepV6ToV7().Migrate(save);

            Assert.IsTrue(result.IsConverted, result.FailureReason);
            Assert.AreEqual(3, result.Save["world"][0]["state"][VanillaKey]["processor"]["inheritedLevel"].Value<int>());
            Assert.IsNull(result.Save["world"][1]["state"]["ChestComponent"]["inheritedLevel"]);
        }

        [Test]
        public void processorがオブジェクトでなければ失敗するTest()
        {
            var save = Save(JObject.Parse($"{{\"state\":{{\"{VanillaKey}\":{{\"processor\":5}}}}}}"));

            var result = new SaveMigrationStepV6ToV7().Migrate(save);

            Assert.IsFalse(result.IsConverted);
            StringAssert.Contains("processor", result.FailureReason);
        }

        [Test]
        public void 変換元の版は6Test()
        {
            Assert.AreEqual(6, new SaveMigrationStepV6ToV7().FromVersion);
        }

        private static JObject Save(params JObject[] blocks)
        {
            return new JObject { ["worldVersion"] = 6, ["world"] = new JArray(blocks) };
        }
    }
}
