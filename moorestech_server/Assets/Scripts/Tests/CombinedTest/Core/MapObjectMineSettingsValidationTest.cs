using System.IO;
using System.Linq;
using Core.Master;
using Core.Master.Validator;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.CombinedTest.Core
{
    public class MapObjectMineSettingsValidationTest
    {
        // テストマスタの装飾物。採掘機の対象に載せてはならない
        // The decoration in the test master; a miner must never list it as a target
        private const string DecorationMapObjectGuid = "00000000-0000-4444-0000-000000000001";

        [Test]
        // 装飾物を指す採掘機設定はBlockMasterバリデーションで検出される
        // A miner setting pointing at a decoration is detected by BlockMaster validation
        public void 採掘機が装飾物を対象に載せると検証で弾かれる()
        {
            PrepareMasterDependencies();
            var blockMaster = CreateBlockMasterPointingMinerAtDecoration();

            var isValid = BlockMasterUtil.Validate(blockMaster.Blocks, out var errorLogs);

            Assert.IsFalse(isValid);
            StringAssert.Contains($"[BlockMaster] Name:TestGearMapObjectMiner points MapObjectMineSettings.MapObjectGuid:{DecorationMapObjectGuid} which forbids mining", errorLogs);
        }

        [Test]
        // 採掘できるmapObjectだけを載せた設定は検証を通る
        // A setting listing only minable map objects passes validation
        public void 採掘できるmapObjectだけの設定は検証を通る()
        {
            PrepareMasterDependencies();
            var blocksJToken = ParseBlocksJson();

            var isValid = BlockMasterUtil.Validate(new BlockMaster(blocksJToken).Blocks, out var errorLogs);

            Assert.IsTrue(isValid, errorLogs);
        }

        private static void PrepareMasterDependencies()
        {
            // MapObjectMasterなどBlockMaster検証の依存マスタを既存の有効Modで初期化する
            // Initialize dependency masters such as MapObjectMaster from the existing valid mod
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        private static BlockMaster CreateBlockMasterPointingMinerAtDecoration()
        {
            // blocks.jsonは変更せず、テスト内のJTokenへ装飾物の設定だけを足す
            // Keep blocks.json unchanged and add only the decoration setting to the in-test JToken
            var blocksJToken = ParseBlocksJson();
            var miner = blocksJToken["data"].Children<JObject>()
                .First(block => (string)block["name"] == "TestGearMapObjectMiner");
            var mineSettings = (JArray)miner["blockParam"]["mapObjectMineSettings"];
            mineSettings.Add(new JObject
            {
                ["attackHp"] = 10,
                ["miningTime"] = 2,
                ["mapObjectGuid"] = DecorationMapObjectGuid,
            });

            return new BlockMaster(blocksJToken);
        }

        private static JToken ParseBlocksJson()
        {
            var blocksJsonPath = Path.Combine(TestModDirectory.ForUnitTestModDirectory, "mods", "forUnitTest", "master", "blocks.json");
            return JToken.Parse(File.ReadAllText(blocksJsonPath));
        }
    }
}
