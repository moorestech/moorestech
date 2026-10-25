using System.IO;
using Core.Master;
using Core.Master.Validator;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Core.Master.OrbitalPayload
{
    public class OrbitalPayloadMasterValidatorTest
    {
        [Test]
        public void テストマスタの積荷表は検証を通る()
        {
            // DIの生成でマスタが読み込まれ、検証エラーなら例外で落ちる
            // Building the container loads the master and would throw on a validation error
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            Assert.AreEqual(6, MasterHolder.ItemMaster.Items.OrbitalPayloads.Length);
        }

        [TestCase("countPerLaunch", 101, "exceeds level-1 stack")]
        [TestCase("countPerLaunch", 0, "must be >= 1")]
        public void 積荷の個数の不正を止める(string key, int value, string expected)
        {
            var json = LoadTestItemsJson();
            json["orbitalPayloads"][0][key] = value;

            var isValid = ItemMasterUtil.Validate(new ItemMaster(json).Items, out var errorLogs);

            Assert.IsFalse(isValid);
            StringAssert.Contains(expected, errorLogs);
        }

        [Test]
        public void 重複した積荷を止める()
        {
            var json = LoadTestItemsJson();
            ((JArray)json["orbitalPayloads"]).Add(json["orbitalPayloads"][0].DeepClone());

            var isValid = ItemMasterUtil.Validate(new ItemMaster(json).Items, out var errorLogs);

            Assert.IsFalse(isValid);
            StringAssert.Contains("duplicate payload", errorLogs);
        }

        [TestCase("payloadItemGuid", "unknown payloadItemGuid")]
        [TestCase("arrivalOutputItemGuid", "unknown arrival output")]
        public void 未定義アイテムへの参照を止める(string field, string expected)
        {
            var json = LoadTestItemsJson();
            // 積荷側と成果側の参照切れを別々に作る
            // Break the payload and arrival-output references independently
            var payload = json["orbitalPayloads"][0];
            var target = field == "payloadItemGuid" ? payload : payload["arrivalOutputs"][0];
            target[field == "payloadItemGuid" ? "payloadItemGuid" : "itemGuid"] = System.Guid.Empty.ToString();

            Assert.IsFalse(ItemMasterUtil.Validate(new ItemMaster(json).Items, out var errorLogs));
            StringAssert.Contains(expected, errorLogs);
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void 成果の個数が正でない積荷を止める(int count)
        {
            var json = LoadTestItemsJson();
            json["orbitalPayloads"][0]["arrivalOutputs"][0]["count"] = count;

            Assert.IsFalse(ItemMasterUtil.Validate(new ItemMaster(json).Items, out var errorLogs));
            StringAssert.Contains("arrival output count must be >= 1", errorLogs);
        }

        private static JObject LoadTestItemsJson()
        {
            var path = Path.Combine(TestModDirectory.ForUnitTestModDirectory, "mods", "forUnitTest", "master", "items.json");
            return JObject.Parse(File.ReadAllText(path));
        }
    }
}
