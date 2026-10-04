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
    ///     接続線用に予約した破壊カテゴリーキーをマスタが使うと検証で弾かれることを確かめる
    ///     Verifies the master is rejected when it uses the destruction category key reserved for connection lines
    /// </summary>
    public class BlockDestructionCategoryReservedKeyTest
    {
        [TestCase(false)]
        [TestCase(true)]
        public void ReservedConnectionLineKeyIsRejected(bool withoutTargets)
        {
            // 依存マスタを既存の有効Modで初期化し、blocks.jsonのカテゴリーキーだけをテスト内で差し替える
            // Initialize dependency masters from the valid mod and replace only the category key in the in-test blocks.json
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
