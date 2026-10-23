using System.Text.RegularExpressions;
using Game.SaveLoad.Migration.Steps;
using NUnit.Framework;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.UnitTest.Game.SaveLoad
{
    public class SaveMigrationStepV7ToV8Test
    {
        [Test]
        public void 節が無ければ0で足す()
        {
            var save = JObject.Parse("{\"worldVersion\":6,\"world\":[]}");
            var result = new SaveMigrationStepV7ToV8().Migrate(save);
            Assert.IsTrue(result.IsConverted, result.FailureReason);
            Assert.AreEqual(0, result.Save["spaceElevatorCablePool"]["cableCount"].Value<int>());
            Assert.IsNull(result.Save["backfilledFields"]);
        }

        [Test]
        public void 正しい節があれば素通しする()
        {
            var save = JObject.Parse("{\"worldVersion\":6,\"spaceElevatorCablePool\":{\"cableCount\":250}}");
            var result = new SaveMigrationStepV7ToV8().Migrate(save);
            Assert.IsTrue(result.IsConverted, result.FailureReason);
            Assert.AreEqual(250, result.Save["spaceElevatorCablePool"]["cableCount"].Value<int>());
        }

        [TestCase("{\"spaceElevatorCablePool\":5}")]
        [TestCase("{\"spaceElevatorCablePool\":{\"cableCount\":\"x\"}}")]
        public void 形の違う節は変換を拒む(string json)
        {
            LogAssert.Expect(LogType.Warning, new Regex("版6から版7へ変換できません"));
            var result = new SaveMigrationStepV7ToV8().Migrate(JObject.Parse(json));
            Assert.IsFalse(result.IsConverted);
        }

        [Test]
        public void FromVersionは6()
        {
            Assert.AreEqual(6, new SaveMigrationStepV7ToV8().FromVersion);
        }
    }
}
