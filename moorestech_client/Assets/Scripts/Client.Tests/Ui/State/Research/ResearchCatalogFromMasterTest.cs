using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Client.Ui.State.Research.Catalog;
using Client.Ui.State.Research.Logic;
using Client.WebUiHost.Game.Topics;
using Core.Master;
using Game.Block.Interface.MachineUpgrade;
using Game.Research;
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

        [Test]
        public void 全ノードがWebの橋のDTOと同じアイコンと前提と消費と解放物を持つ()
        {
            // Web の橋 ResearchNodeDtoFactory の写しなので、橋の読み方の変更や種別の追加の取りこぼしをここで捕まえる（前例 設計書 A-102）
            // This copies the Web bridge ResearchNodeDtoFactory, so catch missed reading changes or new kinds here (precedent design A-102)
            var noStates = new Dictionary<Guid, ResearchNodeState>();
            foreach (var definition in ResearchNodeDefinitionsFromMaster.Build())
            {
                var dto = ResearchNodeDtoFactory.Create(MasterHolder.ResearchMaster.GetResearch(definition.Guid), noStates);
                var label = definition.Guid.ToString();
                Assert.AreEqual(dto.IconItemId, definition.IconItemId.AsPrimitive(), label);
                CollectionAssert.AreEqual(dto.PrevGuids, definition.PreviousGuids.Select(guid => guid.ToString()).ToList(), label);
                CollectionAssert.AreEqual(dto.ConsumeItems.Select(c => Join(c.ItemId, c.Count, JoinInts(c.AcceptedItemIds))), definition.ConsumeItems.Select(c => Join(c.ItemId.AsPrimitive(), c.Count, JoinInts(c.AcceptedItemIds.Select(id => id.AsPrimitive())))), label);
                AssertSameUnlocks(dto, definition.Unlocks, label);
            }
        }

        #region Helpers

        private static void AssertSameUnlocks(ResearchNodeDto dto, ResearchUnlockSet unlocks, string label)
        {
            // 8種を同値で比べる
            // Compare all eight kinds
            CollectionAssert.AreEqual(dto.RewardItems.Select(r => Join(r.ItemId, r.Count)), unlocks.RewardItems.Select(r => Join(r.ItemId.AsPrimitive(), r.Count)), $"{label} reward");
            CollectionAssert.AreEqual(dto.UnlockItemRecipeViewItemIds, unlocks.ItemRecipeViewItemIds.Select(id => id.AsPrimitive()), $"{label} itemRecipeView");
            CollectionAssert.AreEqual(dto.UnlockBlocks.Select(b => b.BlockId), unlocks.Blocks.Select(b => b.BlockId.AsPrimitive()), $"{label} block");
            CollectionAssert.AreEqual(dto.UnlockMachineRecipes.Select(r => Join(r.RecipeGuid, JoinInts(r.OutputItemIds), Join(r.OutputFluids.Select(f => Join(f.FluidId, f.Amount)).ToArray()))), unlocks.MachineRecipes.Select(r => Join(r.RecipeGuid.ToString(), JoinInts(r.OutputItemIds.Select(id => id.AsPrimitive())), Join(r.OutputFluids.Select(f => Join(f.FluidId.AsPrimitive(), f.Amount)).ToArray()))), $"{label} machineRecipe");
            CollectionAssert.AreEqual(dto.UnlockConnectToolGuids, unlocks.ConnectToolGuids.Select(guid => guid.ToString()), $"{label} connectTool");
            CollectionAssert.AreEqual(dto.UnlockTrainCarGuids, unlocks.TrainCarGuids.Select(guid => guid.ToString()), $"{label} trainCar");
            CollectionAssert.AreEqual(dto.UnlockModuleSlotStackLimits, unlocks.ModuleSlotStackLimits, $"{label} moduleSlot");
            CollectionAssert.AreEqual(dto.UnlockLevelInheritanceCraftCaps.Select(c => Join(c.ItemId, c.Level)), unlocks.LevelInheritanceCraftCaps.Select(c => Join(c.ItemId.AsPrimitive(), c.Level)), $"{label} craftCap");
        }

        // int[] を params object[] に渡すと 1 要素に包まれ比較が無効になるため専用に展開する
        // Passing int[] to params object[] wraps it as one element and defeats the comparison, so expand it explicitly
        private static string JoinInts(IEnumerable<int> ids)
        {
            return string.Join(",", ids);
        }

        private static string Join(params object[] parts)
        {
            return string.Join("|", parts.Select(part => Convert.ToString(part, CultureInfo.InvariantCulture)));
        }

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
