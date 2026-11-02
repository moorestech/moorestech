using System;
using System.Collections.Generic;
using System.Linq;
using Client.WebUiHost.Game.Topics;
using Core.Master;
using Game.Research;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Client.Tests.WebUi.Research
{
    /// <summary>
    /// 実マスタ直叩き回帰試験（前例あり）
    /// Regression tests against the real master (precedent exists)
    /// </summary>
    public class ResearchNodeDtoFactoryTest
    {
        // 解放種別カバレッジ用ノード
        // Node covering the unlock kinds
        private static readonly Guid CoverageNodeGuid = Guid.Parse("bb000000-0000-4000-8000-000000000001");

        [Test]
        public void Createはunlockblockのblockidとblockguidを実マスタどおり返す()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var master = MasterHolder.ResearchMaster.ResearchElements[CoverageNodeGuid];

            var dto = ResearchNodeDtoFactory.Create(master, new Dictionary<Guid, ResearchNodeState>());

            var expectedBlockGuid = Guid.Parse("00000000-0000-0000-0000-000000000002");
            Assert.AreEqual(1, dto.UnlockBlocks.Count);
            Assert.AreEqual(MasterHolder.BlockMaster.GetBlockId(expectedBlockGuid).AsPrimitive(), dto.UnlockBlocks[0].BlockId);
            Assert.AreEqual(expectedBlockGuid.ToString(), dto.UnlockBlocks[0].BlockGuid);
        }

        [Test]
        public void Createはunlockmachinerecipeをレシピ単位で出力する()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var master = MasterHolder.ResearchMaster.ResearchElements[CoverageNodeGuid];

            var dto = ResearchNodeDtoFactory.Create(master, new Dictionary<Guid, ResearchNodeState>());

            // 4レシピが平坦化されず個別に残る
            // All 4 recipes survive individually, unflattened
            Assert.AreEqual(4, dto.UnlockMachineRecipes.Count);

            // (a) 通常出力: item 3個
            // (a) normal output: item 3
            var normal = dto.UnlockMachineRecipes.Single(r => r.RecipeGuid == "bd3d4d7d-9c3b-4ae1-875b-950327eedd9d");
            // (b)別レシピの同一出力も残る
            // (b) another recipe's same output also survives
            var duplicateOutput = dto.UnlockMachineRecipes.Single(r => r.RecipeGuid == "7ba3de84-6823-4970-b84a-13e7d8b7bf1d");
            var expectedItem3 = MasterHolder.ItemMaster.GetItemId(Guid.Parse("00000000-0000-0000-1234-000000000003")).AsPrimitive();
            CollectionAssert.AreEqual(new[] { expectedItem3 }, normal.OutputItemIds);
            CollectionAssert.AreEqual(new[] { expectedItem3 }, duplicateOutput.OutputItemIds);
            CollectionAssert.IsEmpty(normal.OutputFluids);

            // (c) 1レシピが複数出力を保持
            // (c) a single recipe retains multiple outputs
            var multiOutput = dto.UnlockMachineRecipes.Single(r => r.RecipeGuid == "ad81ded0-8b7f-40ab-85e3-cff4108479da");
            Assert.AreEqual(2, multiOutput.OutputItemIds.Count);

            // (d) 液体のみレシピも残存
            // (d) a fluid-only recipe survives too
            var fluidOnly = dto.UnlockMachineRecipes.Single(r => r.RecipeGuid == "aa000000-0000-4000-8000-00000000fee1");
            CollectionAssert.IsEmpty(fluidOnly.OutputItemIds);
            Assert.AreEqual(1, fluidOnly.OutputFluids.Count);
            var expectedFluidId = MasterHolder.FluidMaster.GetFluidId(Guid.Parse("00000000-0000-0000-1234-000000000003")).AsPrimitive();
            Assert.AreEqual(expectedFluidId, fluidOnly.OutputFluids[0].FluidId);
            Assert.AreEqual(50, fluidOnly.OutputFluids[0].Amount);
        }

        [Test]
        public void Createは消費ツールチップ引数とセクション対応先を取り違えない()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var master = MasterHolder.ResearchMaster.ResearchElements[CoverageNodeGuid];

            var dto = ResearchNodeDtoFactory.Create(master, new Dictionary<Guid, ResearchNodeState>());

            // 解放種別ごとに対応先セクションが入れ替わっていないことを実マスタで押さえる
            // Pin each unlock kind to its own section against the real master
            Assert.AreEqual(1, dto.UnlockBlocks.Count);
            Assert.AreEqual(4, dto.UnlockMachineRecipes.Count);
            CollectionAssert.AreEqual(new[] { "cc000000-0000-4000-8000-000000000001" }, dto.UnlockConnectToolGuids);
            CollectionAssert.AreEqual(new[] { "dc82cf3f-709d-49eb-bdb2-67ffcaff561b" }, dto.UnlockTrainCarGuids);
            CollectionAssert.IsEmpty(dto.UnlockItemRecipeViewItemIds);
            CollectionAssert.IsEmpty(dto.RewardItems);
        }

        [Test]
        public void Createはスロット上限と製作上限と代替の受け付けIDを出す()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var states = new Dictionary<Guid, ResearchNodeState>();

            // スロット上限はレベルでなく上限値で運ぶ（テストマスタの段階表 [3, 6] の level 1 は 6）
            // The slot limit travels as the limit value, not the level (level 1 of the test table [3, 6] is 6)
            var slot = ResearchNodeDtoFactory.Create(MasterHolder.ResearchMaster.ResearchElements[ForUnitTestLevelInheritanceIds.ModuleSlotStackResearchGuid], states);
            CollectionAssert.AreEqual(new[] { 6 }, slot.UnlockModuleSlotStackLimits);
            CollectionAssert.IsEmpty(slot.UnlockLevelInheritanceCraftCaps);

            // 製作上限は継承先ファミリーの基準アイテムIDと解放後のレベル
            // The craft cap carries the target family's base item ID and the unlocked level
            var cap = ResearchNodeDtoFactory.Create(MasterHolder.ResearchMaster.ResearchElements[ForUnitTestLevelInheritanceIds.CraftCapResearchGuid], states);
            Assert.AreEqual(1, cap.UnlockLevelInheritanceCraftCaps.Count);
            Assert.AreEqual(ForUnitTestLevelInheritanceIds.CappedTarget(1).AsPrimitive(), cap.UnlockLevelInheritanceCraftCaps[0].ItemId);
            Assert.AreEqual(2, cap.UnlockLevelInheritanceCraftCaps[0].Level);

            // 基準を消費する研究は上位の変種も受け付けIDに並ぶ（下位から）。基準でない消費は自分だけ
            // A research consuming a base lists the higher variants too (lowest first); a non-base consume lists only itself
            var substitute = ResearchNodeDtoFactory.Create(MasterHolder.ResearchMaster.ResearchElements[ForUnitTestLevelInheritanceIds.SubstituteResearchGuid], states);
            var sourceIds = new[] { ForUnitTestLevelInheritanceIds.Source(1).AsPrimitive(), ForUnitTestLevelInheritanceIds.Source(2).AsPrimitive(), ForUnitTestLevelInheritanceIds.Source(3).AsPrimitive() };
            CollectionAssert.AreEqual(sourceIds, substitute.ConsumeItems[0].AcceptedItemIds);
            var plain = ResearchNodeDtoFactory.Create(MasterHolder.ResearchMaster.ResearchElements[Guid.Parse("cd05e30d-d599-46d3-a079-769113cbbf17")], states);
            CollectionAssert.AreEqual(new[] { plain.ConsumeItems[0].ItemId }, plain.ConsumeItems[0].AcceptedItemIds);
        }
    }
}
