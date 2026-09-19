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
    ///     採掘機・mapObject採掘機・ポンプの採取設定の起動時検証を確かめる（実行時の読み飛ばしを検証へ付け替えた分）
    ///     Checks the startup validation of miner, map-object miner and pump extraction settings (moved from runtime skipping into validation)
    /// </summary>
    public class ExtractionSettingsValidationTest
    {
        private const string MissingGuid = "00000000-0000-0000-9999-999999999999";

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

            AssertInvalidWith(blocksJToken, "[BlockMaster] Name:TestElectricMiner has non-positive MineSettings.Time:0");
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
        public void ポンプの生成時間が0以下だと検証で弾かれる()
        {
            var blocksJToken = PrepareBlocksJson();
            FindBlockParam(blocksJToken, "ElectricPump")["generateFluid"][0]["generateTime"] = 0;

            AssertInvalidWith(blocksJToken, "[BlockMaster] Name:ElectricPump has non-positive GenerateFluid.GenerateTime:0");
        }

        [Test]
        public void ポンプの生成量が0以下だと検証で弾かれる()
        {
            var blocksJToken = PrepareBlocksJson();
            FindBlockParam(blocksJToken, "GearPump")["generateFluid"][0]["amount"] = -1;

            AssertInvalidWith(blocksJToken, "[BlockMaster] Name:GearPump has non-positive GenerateFluid.Amount:-1");
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
            // ItemMaster・MapObjectMasterなど検証の依存マスタを既存の有効Modで初期化し、blocks.jsonはテスト内のJTokenだけを書き換える
            // Initialize dependency masters such as ItemMaster and MapObjectMaster from the valid mod, then edit only the in-test JToken of blocks.json
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
