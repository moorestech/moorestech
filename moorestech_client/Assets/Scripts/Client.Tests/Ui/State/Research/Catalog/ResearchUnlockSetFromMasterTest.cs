using System;
using System.Linq;
using Client.Ui.State.Research.Catalog;
using Core.Master;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Client.Tests.Ui.State.Research.Catalog
{
    /// <summary>
    /// 研究の解放物を種類ごとの欄に、実マスタどおりの値で入れることを確かめる（L0）
    /// Verifies research unlocks land in their own kind's field with the real master's values (L0)
    /// </summary>
    public class ResearchUnlockSetFromMasterTest
    {
        private static readonly Guid CoverageNodeGuid = Guid.Parse("bb000000-0000-4000-8000-000000000001");
        private static readonly Guid RewardNodeGuid = Guid.Parse("bf9bda9e-dace-43c4-9a33-75f248fd17f6");
        private static readonly Guid PlainConsumeNodeGuid = Guid.Parse("cd05e30d-d599-46d3-a079-769113cbbf17");

        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [Test]
        public void 種類ごとの欄に実マスタどおり入り機械レシピはレシピ単位で残る()
        {
            var unlocks = ResearchUnlockSetFromMaster.Build(MasterHolder.ResearchMaster.ResearchElements[CoverageNodeGuid]);

            var blockGuid = Guid.Parse("00000000-0000-0000-0000-000000000002");
            Assert.AreEqual(1, unlocks.Blocks.Count);
            Assert.AreEqual(MasterHolder.BlockMaster.GetBlockId(blockGuid), unlocks.Blocks[0].BlockId);
            Assert.AreEqual(blockGuid, unlocks.Blocks[0].BlockGuid);
            CollectionAssert.AreEqual(new[] { Guid.Parse("cc000000-0000-4000-8000-000000000001") }, unlocks.ConnectToolGuids);
            CollectionAssert.AreEqual(new[] { Guid.Parse("dc82cf3f-709d-49eb-bdb2-67ffcaff561b") }, unlocks.TrainCarGuids);
            CollectionAssert.IsEmpty(unlocks.ItemRecipeViewItemIds);
            CollectionAssert.IsEmpty(unlocks.RewardItems);

            // 4 レシピが平坦化されず残り、同じ出力の別レシピ・複数出力・液体だけのレシピも保つ
            // All four recipes survive unflattened, keeping the same-output pair, the multi-output and the fluid-only recipe
            Assert.AreEqual(4, unlocks.MachineRecipes.Count);
            var item3 = MasterHolder.ItemMaster.GetItemId(Guid.Parse("00000000-0000-0000-1234-000000000003"));
            var normal = unlocks.MachineRecipes.Single(r => r.RecipeGuid == Guid.Parse("bd3d4d7d-9c3b-4ae1-875b-950327eedd9d"));
            CollectionAssert.AreEqual(new[] { item3 }, normal.OutputItemIds);
            CollectionAssert.IsEmpty(normal.OutputFluids);
            CollectionAssert.AreEqual(new[] { item3 }, unlocks.MachineRecipes.Single(r => r.RecipeGuid == Guid.Parse("7ba3de84-6823-4970-b84a-13e7d8b7bf1d")).OutputItemIds);
            Assert.AreEqual(2, unlocks.MachineRecipes.Single(r => r.RecipeGuid == Guid.Parse("ad81ded0-8b7f-40ab-85e3-cff4108479da")).OutputItemIds.Count);
            var fluidOnly = unlocks.MachineRecipes.Single(r => r.RecipeGuid == Guid.Parse("aa000000-0000-4000-8000-00000000fee1"));
            CollectionAssert.IsEmpty(fluidOnly.OutputItemIds);
            Assert.AreEqual(1, fluidOnly.OutputFluids.Count);
            Assert.AreEqual(MasterHolder.FluidMaster.GetFluidId(Guid.Parse("00000000-0000-0000-1234-000000000003")), fluidOnly.OutputFluids[0].FluidId);
            Assert.AreEqual(50, fluidOnly.OutputFluids[0].Amount);
        }

        [Test]
        public void 報酬の個数とレシピ表示とモジュール枠と製作上限を写す()
        {
            var reward = ResearchUnlockSetFromMaster.Build(MasterHolder.ResearchMaster.ResearchElements[RewardNodeGuid]);
            Assert.AreEqual(1, reward.RewardItems.Count);
            Assert.AreEqual(MasterHolder.ItemMaster.GetItemId(Guid.Parse("00000000-0000-0000-1234-000000000002")), reward.RewardItems[0].ItemId);
            Assert.AreEqual(3, reward.RewardItems[0].Count);
            CollectionAssert.AreEqual(new[] { MasterHolder.ItemMaster.GetItemId(Guid.Parse("00000000-0000-0000-1234-000000000003")) }, reward.ItemRecipeViewItemIds);

            // モジュール枠のノードは製作上限を持たず、製作上限のノードは継承先の基準アイテムと解放後のレベル
            // The module-slot node has no craft cap; the craft-cap node carries the target base item and the unlocked level
            var slot = ResearchUnlockSetFromMaster.Build(MasterHolder.ResearchMaster.ResearchElements[ForUnitTestLevelInheritanceIds.ModuleSlotStackResearchGuid]);
            CollectionAssert.IsEmpty(slot.LevelInheritanceCraftCaps);
            var cap = ResearchUnlockSetFromMaster.Build(MasterHolder.ResearchMaster.ResearchElements[ForUnitTestLevelInheritanceIds.CraftCapResearchGuid]);
            Assert.AreEqual(1, cap.LevelInheritanceCraftCaps.Count);
            Assert.AreEqual(ForUnitTestLevelInheritanceIds.CappedTarget(1), cap.LevelInheritanceCraftCaps[0].ItemId);
            Assert.AreEqual(2, cap.LevelInheritanceCraftCaps[0].Level);
        }

        [Test]
        public void 基準を消費する研究は上位の変種も受け付け基準でない消費は自分だけ()
        {
            var definitions = ResearchNodeDefinitionsFromMaster.Build();

            var substitute = definitions.Single(node => node.Guid == ForUnitTestLevelInheritanceIds.SubstituteResearchGuid).ConsumeItems[0];
            CollectionAssert.AreEqual(new[] { ForUnitTestLevelInheritanceIds.Source(1), ForUnitTestLevelInheritanceIds.Source(2), ForUnitTestLevelInheritanceIds.Source(3) }, substitute.AcceptedItemIds);
            var plain = definitions.Single(node => node.Guid == PlainConsumeNodeGuid).ConsumeItems[0];
            CollectionAssert.AreEqual(new[] { plain.ItemId }, plain.AcceptedItemIds);
        }
    }
}
