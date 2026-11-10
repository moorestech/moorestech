using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Client.Tests.Ui.Content;
using Client.Ui.Content;
using Client.Ui.State.Research.Catalog;
using Client.Ui.State.Research.Logic;
using Client.Ui.State.Research.Tree;
using Core.Master;
using Game.Research;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.Research
{
    /// <summary>
    /// 研究画面L1の共通材料
    /// Shared material for research L1 tests
    /// </summary>
    internal static class ResearchScreenTestKit
    {
        public static readonly Guid Researchable = Guid.Parse("00000000-0000-0000-0000-000000000201");
        public static readonly Guid Completed = Guid.Parse("00000000-0000-0000-0000-000000000202");
        public static readonly Guid ItemLacking = Guid.Parse("00000000-0000-0000-0000-000000000203");
        public static readonly Guid PreNodeLacking = Guid.Parse("00000000-0000-0000-0000-000000000204");
        public static readonly Guid AllLacking = Guid.Parse("00000000-0000-0000-0000-000000000205");
        public static readonly Guid[] AllNodes = { Researchable, Completed, ItemLacking, PreNodeLacking, AllLacking };
        public static readonly Guid WaterFluid = Guid.Parse("00000000-0000-0000-1234-000000000001");
        public static readonly Guid BlockAndToolsResearch = Guid.Parse("bb000000-0000-4000-8000-000000000001");
        public static readonly Guid ModuleSlotResearch = Guid.Parse("c0de0000-0000-4000-8000-000000000002");
        public static readonly Guid CraftCapResearch = Guid.Parse("c0de0000-0000-4000-8000-000000000001");
        public static readonly Guid RewardResearch = Guid.Parse("bf9bda9e-dace-43c4-9a33-75f248fd17f6");

        private static readonly Regex RichTextTag = new("<[^>]+>");

        public static GameContentPresentation LoadMasterAndContent(Texture2D texture)
        {
            // 名前と絵はIDから引く
            // Names and pictures resolve from ids
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            return new GameContentPresentation(new FakeContentTextureSource(texture, texture, texture));
        }

        // 5 状態を 1 枚ずつ持つ木。研究可能が原点、他は上下左右
        // A tree with one card per state; researchable at the origin, the rest around it
        public static ResearchTreeViewModel Tree(ResearchSelection selection)
        {
            var definitions = new List<ResearchNodeDefinition>
            {
                Node(Researchable, Vector2.zero),
                Node(Completed, new Vector2(-220f, 0f)),
                Node(ItemLacking, new Vector2(220f, 0f)),
                Node(PreNodeLacking, new Vector2(0f, 160f), Researchable),
                Node(AllLacking, new Vector2(220f, 160f), ItemLacking),
            };
            var states = new Dictionary<Guid, ResearchNodeState>
            {
                { Researchable, ResearchNodeState.Researchable },
                { Completed, ResearchNodeState.Completed },
                { ItemLacking, ResearchNodeState.UnresearchableNotEnoughItem },
                { PreNodeLacking, ResearchNodeState.UnresearchableNotEnoughPreNode },
                { AllLacking, ResearchNodeState.UnresearchableAllReasons },
            };
            return ResearchTreeViewModelFactory.Create(definitions, states, new Dictionary<ItemId, int>(), selection);
        }

        // 1ノードの詳細（消費はItemId1を3個）
        // Single-node details (3 of ItemId1)
        public static ResearchDetailViewModel Detail(ResearchNodeState state, int ownedItem1, int ownedItem2, ResearchUnlockSet unlocks)
        {
            var consume = new List<ResearchConsumeItem> { new(ForUnitTestItemId.ItemId1, 3, new List<ItemId> { ForUnitTestItemId.ItemId1, ForUnitTestItemId.ItemId2 }) };
            var definition = new ResearchNodeDefinition(Researchable, ForUnitTestItemId.ItemId1, Vector2.zero, new List<Guid>(), consume, unlocks);
            var owned = new Dictionary<ItemId, int> { { ForUnitTestItemId.ItemId1, ownedItem1 }, { ForUnitTestItemId.ItemId2, ownedItem2 } };
            var states = new Dictionary<Guid, ResearchNodeState> { { Researchable, state } };
            var tree = ResearchTreeViewModelFactory.Create(new List<ResearchNodeDefinition> { definition }, states, owned, ResearchSelection.Of(Researchable));
            if (!tree.TryGetDetail(out var detail)) throw new InvalidOperationException("選択したノードの詳細が組まれていない");
            return detail;
        }

        // LocalizedLabel は --localized-label-line-height を <line-height=Npx> で前置するので外す（前例 ChallengeHudTestText.Plain）
        // LocalizedLabel prefixes --localized-label-line-height as <line-height=Npx>, so strip it (precedent ChallengeHudTestText.Plain)
        public static string PlainText(Label label)
        {
            return RichTextTag.Replace(label.text, string.Empty);
        }

        public static ResearchUnlockSet NoUnlocks()
        {
            return Unlocks(new List<UnlockBlock>(), new List<UnlockMachineRecipe>(), new List<ItemId>(), new List<RewardItem>(), new List<Guid>(), new List<Guid>(), new List<int>(), new List<LevelInheritanceCraftCap>());
        }

        public static ResearchUnlockSet Unlocks(IReadOnlyList<UnlockBlock> blocks, IReadOnlyList<UnlockMachineRecipe> machineRecipes, IReadOnlyList<ItemId> itemRecipeViews, IReadOnlyList<RewardItem> rewards, IReadOnlyList<Guid> connectTools, IReadOnlyList<Guid> trainCars, IReadOnlyList<int> moduleSlotLimits, IReadOnlyList<LevelInheritanceCraftCap> craftCaps)
        {
            return new ResearchUnlockSet(blocks, machineRecipes, itemRecipeViews, rewards, connectTools, trainCars, moduleSlotLimits, craftCaps);
        }

        // テストmodの解放物をそのまま引く
        // Take test mod unlocks as they are
        public static ResearchUnlockSet MasterUnlocks(Guid researchGuid)
        {
            return ResearchNodeDefinitionsFromMaster.Build().Single(node => node.Guid == researchGuid).Unlocks;
        }

        private static ResearchNodeDefinition Node(Guid guid, Vector2 position, params Guid[] previous)
        {
            return new ResearchNodeDefinition(guid, ForUnitTestItemId.ItemId1, position, previous, new List<ResearchConsumeItem>(), NoUnlocks());
        }
    }
}
