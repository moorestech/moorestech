using System;
using System.Collections.Generic;
using System.Linq;
using Client.Ui.State.Research.Catalog;
using Client.Ui.State.Research.Logic;
using Core.Master;
using Game.Block.Interface.MachineUpgrade;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Client.Tests.Ui.State.Research
{
    /// <summary>
    /// 研究マスタから定義と解放物を写す
    /// Copies definitions and unlocks from master
    /// </summary>
    public class ResearchCatalogFromMasterTest
    {
        private static readonly Guid FirstResearch = Guid.Parse("cd05e30d-d599-46d3-a079-769113cbbf17");
        private static readonly Guid SecondResearch = Guid.Parse("7f1464a7-ba55-4b96-9257-cfdeddf5bbdd");
        private static readonly Guid BlockAndToolsResearch = Guid.Parse("bb000000-0000-4000-8000-000000000001");
        private static readonly Guid ModuleSlotResearch = Guid.Parse("c0de0000-0000-4000-8000-000000000002");
        private static readonly Guid CraftCapResearch = Guid.Parse("c0de0000-0000-4000-8000-000000000001");
        private static readonly Guid RewardResearch = Guid.Parse("bf9bda9e-dace-43c4-9a33-75f248fd17f6");

        [SetUp]
        public void LoadTestMaster()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [Test]
        public void 全ノードを座標と前提つきで写す()
        {
            var nodes = ResearchNodeDefinitionsFromMaster.Build();

            // マスタ件数・前提GUIDが一致
            // Count and prerequisite GUIDs match
            Assert.AreEqual(MasterHolder.ResearchMaster.GetAllResearches().Count, nodes.Count);
            var second = nodes.Single(node => node.Guid == SecondResearch);
            CollectionAssert.AreEqual(new[] { FirstResearch }, second.PreviousGuids);
        }

        [Test]
        public void ブロックと機械レシピと接続ツールと車両の解放を写す()
        {
            var unlocks = UnlocksOf(BlockAndToolsResearch);

            Assert.IsNotEmpty(unlocks.Blocks);
            Assert.IsNotEmpty(unlocks.MachineRecipes);
            Assert.IsNotEmpty(unlocks.ConnectToolGuids);
            Assert.IsNotEmpty(unlocks.TrainCarGuids);
            // 出力を1つ以上運ぶ
            // Carries at least one output
            var recipe = unlocks.MachineRecipes[0];
            Assert.Greater(recipe.OutputItemIds.Count + recipe.OutputFluids.Count, 0);
        }

        [Test]
        public void モジュール枠はレベルでなく上限値で製作上限はアイテムとレベルで写す()
        {
            var moduleSlot = UnlocksOf(ModuleSlotResearch);
            var craftCap = UnlocksOf(CraftCapResearch);

            Assert.AreEqual(1, moduleSlot.ModuleSlotStackLimits.Count);
            Assert.AreEqual(ModuleSlotStackLimitMasterUtil.GetStackLimit(MasterLevelOf(ModuleSlotResearch)), moduleSlot.ModuleSlotStackLimits[0]);
            Assert.IsNotEmpty(craftCap.LevelInheritanceCraftCaps);
            Assert.AreEqual(2, craftCap.LevelInheritanceCraftCaps[0].Level);
        }

        [Test]
        public void 報酬とレシピ表示の解放を写す()
        {
            var unlocks = UnlocksOf(RewardResearch);

            Assert.IsNotEmpty(unlocks.RewardItems);
            Assert.IsNotEmpty(unlocks.ItemRecipeViewItemIds);
            Assert.Greater(unlocks.RewardItems[0].Count, 0);
        }

        #region Helpers

        private static ResearchUnlockSet UnlocksOf(Guid researchGuid)
        {
            return ResearchNodeDefinitionsFromMaster.Build().Single(node => node.Guid == researchGuid).Unlocks;
        }

        private static int MasterLevelOf(Guid researchGuid)
        {
            var master = MasterHolder.ResearchMaster.GetAllResearches().Single(node => node.ResearchNodeGuid == researchGuid);
            var action = master.ClearedActions.items.Single(item => item.GameActionType == Mooresmaster.Model.GameActionModule.GameActionElement.GameActionTypeConst.unlockModuleSlotStackLevel);
            return ((Mooresmaster.Model.GameActionModule.UnlockModuleSlotStackLevelGameActionParam)action.GameActionParam).Level;
        }

        #endregion
    }
}
