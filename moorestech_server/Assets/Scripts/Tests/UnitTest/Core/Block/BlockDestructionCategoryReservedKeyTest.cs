using System.IO;
using Core.Master;
using Core.Master.Validator;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Core.Block
{
    /// <summary>
    ///     予約済みカテゴリーキーの使用が弾かれることを検証
    ///     Verifies the master is rejected when it uses the reserved category key
    /// </summary>
    public class BlockDestructionCategoryReservedKeyTest
    {
        [TestCase(false)]
        [TestCase(true)]
        public void ReservedConnectionLineKeyIsRejected(bool withoutTargets)
        {
            // 有効Modで初期化しキーだけ差し替える
            // Initialize from the valid mod and replace only the category key
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var blocksJsonPath = Path.Combine(TestModDirectory.ForUnitTestModDirectory, "mods", "forUnitTest", "master", "blocks.json");
            var blocksJToken = JToken.Parse(File.ReadAllText(blocksJsonPath));
            blocksJToken["blockDestructionCategories"][0]["categoryKey"] = BlockMaster.ConnectionLineDestructionCategory;
            if (withoutTargets) blocksJToken["blockDestructionCategories"][0]["targetBlocks"] = new JArray();

            var isValid = BlockMasterUtil.Validate(new BlockMaster(blocksJToken).Blocks, out var errorLogs);

            Assert.IsFalse(isValid);
            StringAssert.Contains($"uses the reserved key {BlockMaster.ConnectionLineDestructionCategory}", errorLogs);
        }

        [Test]
        public void ExistingCategoriesPassValidation()
        {
            // 既存のテストmodのカテゴリー定義は予約キーと衝突しない
            // The test mod's existing category definitions do not collide with the reserved key
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            Assert.IsTrue(BlockMasterUtil.Validate(MasterHolder.BlockMaster.Blocks, out var errorLogs), errorLogs);
        }
    }
}
