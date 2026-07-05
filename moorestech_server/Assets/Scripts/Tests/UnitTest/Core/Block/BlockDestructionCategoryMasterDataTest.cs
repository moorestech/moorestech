using System;
using Core.Master;
using NUnit.Framework;
using Server.Boot;
using Tests.Module;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Core.Block
{
    /// <summary>
    ///     破壊カテゴリ定義（blockDestructionCategories）からの逆引きを検証するテスト
    ///     Tests verifying the reverse lookup built from blockDestructionCategories definitions
    /// </summary>
    public class BlockDestructionCategoryMasterDataTest
    {
        // ForUnitTestモッドで foundation カテゴリに登録済みのブロックGUID
        // Block GUIDs registered under the foundation category in the ForUnitTest mod
        private static readonly Guid FoundationBlockA = Guid.Parse("00000000-0000-0000-0000-000000000002");
        private static readonly Guid FoundationBlockB = Guid.Parse("00000000-0000-0000-0000-000000000004");

        // どのカテゴリにも登録されていないブロックGUID（default扱いになるべき）
        // A block GUID absent from every category (should resolve to default)
        private static readonly Guid UnlistedBlock = Guid.Parse("00000000-0000-0000-0000-000000000001");

        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [Test]
        public void RegisteredBlocksResolveToTheirCategory()
        {
            // 定義に登録したブロックはそのカテゴリキーを返す
            // Blocks listed in a definition return that category key
            Assert.AreEqual("foundation", MasterHolder.BlockMaster.GetDestructionCategory(FoundationBlockA));
            Assert.AreEqual("foundation", MasterHolder.BlockMaster.GetDestructionCategory(FoundationBlockB));
        }

        [Test]
        public void UnlistedBlockResolvesToDefault()
        {
            // どの定義にも無いブロックはdefaultにフォールバックする
            // A block in no definition falls back to default
            Assert.AreEqual(BlockMaster.DefaultDestructionCategory, MasterHolder.BlockMaster.GetDestructionCategory(UnlistedBlock));
        }
    }
}
