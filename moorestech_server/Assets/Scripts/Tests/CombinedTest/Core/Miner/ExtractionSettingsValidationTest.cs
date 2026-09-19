using System.IO;
using System.Linq;
using Core.Master;
using Core.Master.Validator;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.CombinedTest.Core.Miner
{
    /// <summary>
    ///     採取設定の起動時検証を確認する
    ///     Checks the startup validation of extraction settings
    /// </summary>
    public class ExtractionSettingsValidationTest
    {
        private const string MissingGuid = "00000000-0000-0000-9999-999999999999";

        // テストマスタの装飾物（採掘対象禁止）
        // The decoration in the test master (must never be a miner target)
        private const string DecorationMapObjectGuid = "00000000-0000-4444-0000-000000000001";

        [Test]
        public void 採掘設定が存在しないアイテムを指すと検証で弾かれる()
        {
            var blocksJToken = PrepareBlocksJson();
            FindBlockParam(blocksJToken, "TestElectricMiner")["mineSettings"][0]["itemGuid"] = MissingGuid;

            AssertInvalidWith(blocksJToken, $"[BlockMaster] Name:TestElectricMiner has invalid MineSettings.ItemGuid:{MissingGuid}");
        }

        [Test]
        public void 採掘時間が0以下だと検証で弾かれる()
        {
            var blocksJToken = PrepareBlocksJson();
            FindBlockParam(blocksJToken, "TestElectricMiner")["mineSettings"][0]["time"] = 0;

            AssertInvalidWith(blocksJToken, "[BlockMaster] Name:TestElectricMiner has non-positive or non-finite MineSettings.Time:0");
        }

        [Test]
        public void 採掘時間がNaNだと検証で弾かれる()
        {
            var blocksJToken = PrepareBlocksJson();
            FindBlockParam(blocksJToken, "TestElectricMiner")["mineSettings"][0]["time"] = double.NaN;

            AssertInvalidWith(blocksJToken, "[BlockMaster] Name:TestElectricMiner has non-positive or non-finite MineSettings.Time:NaN");
        }

        [Test]
        public void mapObject採掘設定が存在しないmapObjectを指すと検証で弾かれる()
        {
            var blocksJToken = PrepareBlocksJson();
            var mineSettings = (JArray)FindBlockParam(blocksJToken, "TestGearMapObjectMiner")["mapObjectMineSettings"];
            mineSettings.Add(new JObject { ["attackHp"] = 10, ["miningTime"] = 2, ["mapObjectGuid"] = MissingGuid });

            AssertInvalidWith(blocksJToken, $"[BlockMaster] Name:TestGearMapObjectMiner has invalid MapObjectMineSettings.MapObjectGuid:{MissingGuid}");
        }

        [Test]
        // 装飾物を指す採掘機設定はBlockMasterバリデーションで検出される
        // A miner setting pointing at a decoration is detected by BlockMaster validation
        public void 採掘機が装飾物を対象に載せると検証で弾かれる()
        {
            var blocksJToken = PrepareBlocksJson();
            var mineSettings = (JArray)FindBlockParam(blocksJToken, "TestGearMapObjectMiner")["mapObjectMineSettings"];
            mineSettings.Add(new JObject { ["attackHp"] = 10, ["miningTime"] = 2, ["mapObjectGuid"] = DecorationMapObjectGuid });

            AssertInvalidWith(blocksJToken, $"[BlockMaster] Name:TestGearMapObjectMiner points MapObjectMineSettings.MapObjectGuid:{DecorationMapObjectGuid} which forbids mining");
        }

        [Test]
        public void ポンプの生成時間が0以下だと検証で弾かれる()
        {
            var blocksJToken = PrepareBlocksJson();
            FindBlockParam(blocksJToken, "ElectricPump")["generateFluid"][0]["generateTime"] = 0;

            AssertInvalidWith(blocksJToken, "[BlockMaster] Name:ElectricPump has non-positive or non-finite GenerateFluid.GenerateTime:0");
        }

        [Test]
        public void ポンプの生成量が0以下だと検証で弾かれる()
        {
            var blocksJToken = PrepareBlocksJson();
            FindBlockParam(blocksJToken, "GearPump")["generateFluid"][0]["amount"] = 0;

            AssertInvalidWith(blocksJToken, "[BlockMaster] Name:GearPump has non-positive or non-finite GenerateFluid.Amount:0");
        }

        [Test]
        public void generateFluidに同一流体が複数あると検証エラーになる()
        {
            var blocksJToken = PrepareBlocksJson();
            var generateFluid = (JArray)FindBlockParam(blocksJToken, "ElectricPump")["generateFluid"];
            var duplicatedFluidGuid = (string)generateFluid[0]["fluidGuid"];
            generateFluid.Add(new JObject { ["fluidGuid"] = duplicatedFluidGuid, ["amount"] = 5, ["generateTime"] = 1 });

            AssertInvalidWith(blocksJToken, $"[BlockMaster] Name:ElectricPump has duplicated GenerateFluid.FluidGuid:{duplicatedFluidGuid}");
        }

        [Test]
        public void 既存テストマスタの採取設定は検証を通る()
        {
            var blocksJToken = PrepareBlocksJson();

            var isValid = BlockMasterUtil.Validate(new BlockMaster(blocksJToken).Blocks, out var errorLogs);

            Assert.IsTrue(isValid, errorLogs);
        }

        private static JToken PrepareBlocksJson()
        {
            // 依存マスタ初期化しJTokenのみ変更
            // Initialize dependency masters, then edit only the in-test JToken
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var blocksJsonPath = Path.Combine(TestModDirectory.ForUnitTestModDirectory, "mods", "forUnitTest", "master", "blocks.json");
            return JToken.Parse(File.ReadAllText(blocksJsonPath));
        }

        private static JToken FindBlockParam(JToken blocksJToken, string blockName)
        {
            return blocksJToken["data"].Children<JObject>().First(block => (string)block["name"] == blockName)["blockParam"];
        }

        private static void AssertInvalidWith(JToken blocksJToken, string expectedLog)
        {
            var isValid = BlockMasterUtil.Validate(new BlockMaster(blocksJToken).Blocks, out var errorLogs);

            Assert.IsFalse(isValid);
            StringAssert.Contains(expectedLog, errorLogs);
        }
    }
}
