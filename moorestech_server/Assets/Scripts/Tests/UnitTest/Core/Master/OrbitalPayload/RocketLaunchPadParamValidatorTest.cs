using System.IO;
using System.Linq;
using Core.Master;
using Core.Master.Validator;
using Mod.Config;
using Mod.Loader;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Core.Master.OrbitalPayload
{
    public class RocketLaunchPadParamValidatorTest
    {
        private const string PadName = "TestRocketLaunchPad";

        [Test]
        public void 出力口に1便ぶんの成果が入りきらない台を止める()
        {
            var blocksJToken = PrepareBlocksJson();
            FindBlockParam(blocksJToken)["outputSlotCount"] = 1;

            var isValid = BlockMasterUtil.Validate(new BlockMaster(blocksJToken).Blocks, out var errorLogs);

            // 成果150はレベル1のスタック100で2スタックになる
            // Output 150 needs 2 stacks at the level-1 stack of 100
            Assert.IsFalse(isValid);
            StringAssert.Contains("cannot hold one launch of payload 5c8e0000-0000-4000-8000-000000000004 (2 stacks at level-1 stack)", errorLogs);
        }

        [Test]
        public void 資材の個数がスタックを超える台を止める()
        {
            var blocksJToken = PrepareBlocksJson();
            FindBlockParam(blocksJToken)["launchMaterials"][0]["count"] = 101;

            var isValid = BlockMasterUtil.Validate(new BlockMaster(blocksJToken).Blocks, out var errorLogs);

            Assert.IsFalse(isValid);
            StringAssert.Contains("exceeds level-1 stack 100", errorLogs);
        }

        [Test]
        public void テストマスタの発射台は検証を通る()
        {
            var blocksJToken = PrepareBlocksJson();

            var isValid = BlockMasterUtil.Validate(new BlockMaster(blocksJToken).Blocks, out var errorLogs);

            Assert.IsTrue(isValid, errorLogs);
        }

        [TestCase("requiredPower", 0)]
        [TestCase("requiredPower", -1)]
        [TestCase("buildSeconds", 0)]
        [TestCase("buildSeconds", -1)]
        [TestCase("launchSeconds", 0)]
        [TestCase("launchSeconds", -1)]
        [TestCase("outputSlotCount", 0)]
        [TestCase("requiredPower", float.NaN)]
        [TestCase("requiredPower", float.PositiveInfinity)]
        [TestCase("requiredPower", float.NegativeInfinity)]
        [TestCase("buildSeconds", float.NaN)]
        [TestCase("buildSeconds", float.PositiveInfinity)]
        [TestCase("buildSeconds", float.NegativeInfinity)]
        [TestCase("launchSeconds", float.NaN)]
        [TestCase("launchSeconds", float.PositiveInfinity)]
        [TestCase("launchSeconds", float.NegativeInfinity)]
        public void 正でない発射台パラメータを止める(string key, float value)
        {
            var blocks = PrepareBlocksJson();
            FindBlockParam(blocks)[key] = value;

            Assert.IsFalse(BlockMasterUtil.Validate(new BlockMaster(blocks).Blocks, out var errors));
            StringAssert.Contains(key + " must be", errors);
        }

        [Test]
        public void 空の資材表を止める()
        {
            var blocks = PrepareBlocksJson();
            FindBlockParam(blocks)["launchMaterials"] = new JArray();

            Assert.IsFalse(BlockMasterUtil.Validate(new BlockMaster(blocks).Blocks, out var errors));
            StringAssert.Contains("launchMaterials must not be empty", errors);
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void 正でない資材個数を止める(int count)
        {
            var blocks = PrepareBlocksJson();
            FindBlockParam(blocks)["launchMaterials"][0]["count"] = count;

            Assert.IsFalse(BlockMasterUtil.Validate(new BlockMaster(blocks).Blocks, out var errors));
            StringAssert.Contains("launch material count must be >= 1", errors);
        }

        [Test]
        public void 重複資材を止める()
        {
            var blocks = PrepareBlocksJson();
            var materials = (JArray)FindBlockParam(blocks)["launchMaterials"];
            materials.Add(materials[0].DeepClone());

            Assert.IsFalse(BlockMasterUtil.Validate(new BlockMaster(blocks).Blocks, out var errors));
            StringAssert.Contains("duplicate launch material", errors);
        }

        [TestCase(false, "unknown launch material")]
        [TestCase(true, "is also an orbital payload")]
        public void 未定義または積荷と同じ資材を止める(bool usePayload, string expected)
        {
            var blocks = PrepareBlocksJson();
            // 既存IDの取得元はテストIDに集約する
            // Resolve existing item identities through the test ID catalog
            var guid = usePayload
                ? MasterHolder.ItemMaster.GetItemMaster(ForUnitTestItemId.SatellitePayloadId).ItemGuid
                : System.Guid.Empty;
            FindBlockParam(blocks)["launchMaterials"][0]["itemGuid"] = guid.ToString();

            Assert.IsFalse(BlockMasterUtil.Validate(new BlockMaster(blocks).Blocks, out var errors));
            StringAssert.Contains(expected, errors);
        }

        [Test]
        public void 積荷表が空なら発射台を止める()
        {
            var resource = new ModsResource(Path.Combine(TestModDirectory.ForUnitTestModDirectory, "mods"));
            var configs = ModJsonStringLoader.GetMasterString(resource);
            var fileName = new JsonFileName("items");
            var items = JObject.Parse(configs[0].JsonContents[fileName]);
            items["orbitalPayloads"] = new JArray();
            configs[0].JsonContents[fileName] = items.ToString();

            // 通常読込が検証エラーで止まるか検査
            // Check that validation errors stop normal loading
            var error = Assert.Throws<System.InvalidOperationException>(() => MasterHolder.Load(new MasterJsonFileContainer(configs)));
            StringAssert.Contains("items.orbitalPayloads is empty", error.Message);
        }

        [Test]
        public void 整数上限の成果数でも容量不足を止める()
        {
            var resource = new ModsResource(Path.Combine(TestModDirectory.ForUnitTestModDirectory, "mods"));
            var configs = ModJsonStringLoader.GetMasterString(resource);
            var fileName = new JsonFileName("items");
            var items = JObject.Parse(configs[0].JsonContents[fileName]);
            items["orbitalPayloads"][0]["arrivalOutputs"][0]["count"] = int.MaxValue;
            configs[0].JsonContents[fileName] = items.ToString();

            // 切り上げ加算のオーバーフローで容量検証をすり抜けない
            // Ceiling-division addition must not overflow and bypass the capacity guard
            var error = Assert.Throws<System.InvalidOperationException>(() => MasterHolder.Load(new MasterJsonFileContainer(configs)));
            StringAssert.Contains("21474837 stacks at level-1 stack", error.Message);
        }

        private static JToken PrepareBlocksJson()
        {
            // 依存マスタ初期化しJTokenのみ変更
            // Initialize dependency masters, then edit only the in-test JToken
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var blocksJsonPath = Path.Combine(TestModDirectory.ForUnitTestModDirectory, "mods", "forUnitTest", "master", "blocks.json");
            return JToken.Parse(File.ReadAllText(blocksJsonPath));
        }

        private static JToken FindBlockParam(JToken blocksJToken)
        {
            return blocksJToken["data"].Children<JObject>().Single(block => (string)block["name"] == PadName)["blockParam"];
        }
    }
}
