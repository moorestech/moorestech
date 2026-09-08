using System.IO;
using Core.Master;
using Core.Master.Validator;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.CombinedTest.Core
{
    /// <summary>
    ///     generateFluidの同一流体重複がマスタ検証で弾かれることを検証する
    ///     Verifies that duplicated fluids in generateFluid are rejected by master validation
    /// </summary>
    public class PumpGenerateFluidValidationTest
    {
        private const string WaterFluidGuid = "00000000-0000-0000-1234-000000000001";

        [Test]
        public void generateFluidに同一流体が複数あると検証エラーになる()
        {
            PrepareMasterDependencies();
            var blockMaster = CreateBlockMasterWithDuplicatedGenerateFluid();

            var isValid = BlockMasterUtil.Validate(blockMaster.Blocks, out var errorLogs);

            Assert.IsFalse(isValid);
            StringAssert.Contains($"[BlockMaster] Name:ElectricPump has duplicated GenerateFluid.FluidGuid:{WaterFluidGuid}", errorLogs);
        }

        [Test]
        public void 既存マスタのgenerateFluidは重複していない()
        {
            PrepareMasterDependencies();
            var blocksJToken = JToken.Parse(File.ReadAllText(BlocksJsonPath()));

            var isValid = BlockMasterUtil.Validate(new BlockMaster(blocksJToken).Blocks, out var errorLogs);

            Assert.IsTrue(isValid, errorLogs);
        }

        private static void PrepareMasterDependencies()
        {
            // FluidMasterなどBlockMaster検証の依存マスタを既存の有効Modで初期化する
            // Initialize dependency masters such as FluidMaster from the existing valid mod
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        private static BlockMaster CreateBlockMasterWithDuplicatedGenerateFluid()
        {
            // blocks.jsonは変更せず、テスト内のJTokenだけを差し替える
            // Keep blocks.json unchanged and replace only the in-test JToken value
            var blocksJToken = JToken.Parse(File.ReadAllText(BlocksJsonPath()));
            var targetBlock = FindBlockByName(blocksJToken, "ElectricPump");
            targetBlock["blockParam"]["generateFluid"] = new JArray(
                CreateGenerateFluid(10, 4),
                CreateGenerateFluid(5, 1));
            return new BlockMaster(blocksJToken);
        }

        private static JObject CreateGenerateFluid(double amount, double generateTime)
        {
            return new JObject
            {
                ["fluidGuid"] = WaterFluidGuid,
                ["amount"] = amount,
                ["generateTime"] = generateTime,
            };
        }

        private static string BlocksJsonPath()
        {
            return Path.Combine(TestModDirectory.ForUnitTestModDirectory, "mods", "forUnitTest", "master", "blocks.json");
        }

        private static JToken FindBlockByName(JToken blocksJToken, string blockName)
        {
            // data配列から対象ブロックを名前で取得する
            // Fetch the target block from the data array by name
            foreach (var block in blocksJToken["data"])
                if (block["name"].Value<string>() == blockName)
                    return block;
            Assert.Fail($"Block not found: {blockName}");
            return null;
        }
    }
}
