using System.Text.RegularExpressions;
using Game.SaveLoad.Migration.Steps;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.UnitTest.Game.SaveLoad.Migration
{
    public class SaveMigrationStepV7ToV8RocketHistoryTest
    {
        [Test]
        public void 版6のセーブに未打ち上げの履歴が足され他の節は残る()
        {
            var save = JObject.Parse("{\"worldVersion\":6,\"world\":[{\"blockGuid\":\"x\"}],\"cleanRoomRooms\":[]}");
            var original = (JObject)save.DeepClone();

            // 履歴以外の木と版番号はステップで書き換えない
            // The step must not change other sections or stamp a version
            var result = new SaveMigrationStepV6ToV7().Migrate(save);

            Assert.IsTrue(result.IsConverted, result.FailureReason);
            Assert.AreEqual(JTokenType.Boolean, result.Save["rocketHasLaunchedOnce"].Type);
            Assert.IsFalse(result.Save["rocketHasLaunchedOnce"].Value<bool>());
            var withoutHistory = (JObject)result.Save.DeepClone();
            withoutHistory.Remove("rocketHasLaunchedOnce");
            Assert.IsTrue(JToken.DeepEquals(original, withoutHistory));
        }

        [Test]
        public void nullの履歴は未打ち上げで補う()
        {
            var save = JObject.Parse("{\"worldVersion\":6,\"rocketHasLaunchedOnce\":null}");

            var result = new SaveMigrationStepV6ToV7().Migrate(save);

            Assert.IsTrue(result.IsConverted, result.FailureReason);
            Assert.IsFalse(result.Save["rocketHasLaunchedOnce"].Value<bool>());
        }

        [TestCase("true")]
        [TestCase("false")]
        [TestCase("1")]
        [TestCase("\"launched\"")]
        [TestCase("{}")]
        [TestCase("[]")]
        public void 履歴が既にある版6は上書きせず変換しない(string historyJson)
        {
            var save = JObject.Parse($"{{\"worldVersion\":6,\"rocketHasLaunchedOnce\":{historyJson}}}");
            var original = (JObject)save.DeepClone();
            LogAssert.Expect(LogType.Warning, new Regex("版6から版7へ変換できません"));

            // 想定外の旧版は理由付きで拒否し、既存値を失わない
            // Refuse an unexpected legacy shape with a reason, preserving its existing value
            var result = new SaveMigrationStepV6ToV7().Migrate(save);

            Assert.IsFalse(result.IsConverted);
            StringAssert.Contains("rocketHasLaunchedOnce", result.FailureReason);
            Assert.IsTrue(JToken.DeepEquals(original, save));
        }

        [Test]
        public void FromVersionは6である()
        {
            Assert.AreEqual(6, new SaveMigrationStepV6ToV7().FromVersion);
        }
    }
}
