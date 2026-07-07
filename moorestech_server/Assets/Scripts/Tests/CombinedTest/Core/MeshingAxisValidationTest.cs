using System.IO;
using Core.Master;
using Core.Master.Validator;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.CombinedTest.Core
{
    public class MeshingAxisValidationTest
    {
        [TestCase(0, 0, 0)]
        [TestCase(1, 1, 0)]
        [TestCase(0, 0, 2)]
        // 不正な噛み合い軸はBlockMasterバリデーションで検出される
        // Invalid meshing axes are detected by BlockMaster validation
        public void InvalidMeshingAxisReturnsValidationErrorTest(int x, int y, int z)
        {
            PrepareMasterDependencies();
            var blockMaster = CreateBlockMasterWithSmallGearMeshingAxis(x, y, z);

            // バリデーション結果とログ内容を同時に確認する
            // Check both the validation result and the emitted log content
            var isValid = BlockMasterUtil.Validate(blockMaster.Blocks, out var errorLogs);

            Assert.IsFalse(isValid);
            StringAssert.Contains("[BlockMaster] Name:SmallTestGear has invalid meshingAxis:", errorLogs);
            StringAssert.Contains("(must be an axis-aligned unit vector, e.g. (0,0,1))", errorLogs);
        }

        [Test]
        // 軸整列単位ベクトルは正常な噛み合い軸として受け入れられる
        // Axis-aligned unit vectors are accepted as valid meshing axes
        public void AxisAlignedUnitMeshingAxisPassesValidationTest()
        {
            PrepareMasterDependencies();
            var blockMaster = CreateBlockMasterWithSmallGearMeshingAxis(0, 0, 1);

            // 既存の有効マスタから対象軸だけを明示的に設定して成功を確認する
            // Confirm success after explicitly setting only the target axis on the existing valid master
            var isValid = BlockMasterUtil.Validate(blockMaster.Blocks, out var errorLogs);

            Assert.IsTrue(isValid, errorLogs);
        }

        private static void PrepareMasterDependencies()
        {
            // ItemMasterなどBlockMaster検証の依存マスタを既存の有効Modで初期化する
            // Initialize dependency masters such as ItemMaster from the existing valid mod
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        private static BlockMaster CreateBlockMasterWithSmallGearMeshingAxis(int x, int y, int z)
        {
            // blocks.jsonは変更せず、テスト内のJTokenだけを差し替える
            // Keep blocks.json unchanged and replace only the in-test JToken value
            var blocksJsonPath = Path.Combine(TestModDirectory.ForUnitTestModDirectory, "mods", "forUnitTest", "master", "blocks.json");
            var blocksJToken = JToken.Parse(File.ReadAllText(blocksJsonPath));
            var targetBlock = FindBlockByName(blocksJToken, "SmallTestGear");
            targetBlock["blockParam"]["gear"]["gearConnects"][0]["option"]["meshingAxis"] = new JArray(x, y, z);
            return new BlockMaster(blocksJToken);
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
