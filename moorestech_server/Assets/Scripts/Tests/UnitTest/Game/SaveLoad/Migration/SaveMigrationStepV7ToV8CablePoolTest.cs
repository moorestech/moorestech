using System.Text.RegularExpressions;
using Game.SaveLoad.Migration.Steps;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.UnitTest.Game.SaveLoad.Migration
{
    public class SaveMigrationStepV7ToV8CablePoolTest
    {
        [Test]
        public void 節が無ければ0で足すTest()
        {
            var result = new SaveMigrationStepV7ToV8().Migrate(JObject.Parse("{\"worldVersion\":7,\"world\":[]}"));

            Assert.IsTrue(result.IsConverted, result.FailureReason);
            Assert.AreEqual(0, result.Save["spaceElevatorCablePool"]["cableCount"].Value<int>());
            Assert.IsNull(result.Save["backfilledFields"]);
        }

        [Test]
        public void 明示nullの節も0で足すTest()
        {
            var result = new SaveMigrationStepV7ToV8().Migrate(JObject.Parse("{\"worldVersion\":7,\"world\":[],\"spaceElevatorCablePool\":null}"));

            Assert.IsTrue(result.IsConverted, result.FailureReason);
            Assert.AreEqual(0, result.Save["spaceElevatorCablePool"]["cableCount"].Value<int>());
        }

        [Test]
        public void 正しい節があれば素通しするTest()
        {
            var result = new SaveMigrationStepV7ToV8().Migrate(JObject.Parse("{\"worldVersion\":7,\"world\":[],\"spaceElevatorCablePool\":{\"cableCount\":250}}"));

            Assert.IsTrue(result.IsConverted, result.FailureReason);
            Assert.AreEqual(250, result.Save["spaceElevatorCablePool"]["cableCount"].Value<int>());
        }

        [TestCase("{\"world\":[],\"spaceElevatorCablePool\":5}")]
        [TestCase("{\"world\":[],\"spaceElevatorCablePool\":{\"cableCount\":\"x\"}}")]
        [TestCase("{\"world\":[],\"spaceElevatorCablePool\":{}}")]
        public void 形の違う節は変換を拒むTest(string json)
        {
            var save = JObject.Parse(json);
            var original = save.DeepClone();
            LogAssert.Expect(LogType.Warning, new Regex("版7から版8へ変換できません"));

            var result = new SaveMigrationStepV7ToV8().Migrate(save);

            Assert.IsFalse(result.IsConverted);
            StringAssert.Contains("spaceElevatorCablePool", result.FailureReason);
            Assert.IsTrue(JToken.DeepEquals(original, save));
        }
    }
}
