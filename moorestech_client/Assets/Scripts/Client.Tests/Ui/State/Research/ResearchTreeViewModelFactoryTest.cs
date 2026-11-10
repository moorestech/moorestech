using System;
using System.Collections.Generic;
using Client.Ui.State.Research.Catalog;
using Client.Ui.State.Research.Logic;
using Client.Ui.State.Research.Tree;
using Core.Master;
using Game.Research;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Ui.State.Research
{
    /// <summary>
    /// 研究ツリーのViewModelを組む（L0）
    /// Builds the research tree view model (L0)
    /// </summary>
    public class ResearchTreeViewModelFactoryTest
    {
        private static readonly Guid Root = Guid.Parse("00000000-0000-0000-0000-000000000101");
        private static readonly Guid Child = Guid.Parse("00000000-0000-0000-0000-000000000102");
        private static readonly ItemId Ore = new(1);
        private static readonly ItemId OreLevel2 = new(2);

        [Test]
        public void 状態の無いノードは全条件未達で描き注目点も無い()
        {
            var model = ResearchTreeViewModelFactory.Create(Definitions(), new Dictionary<Guid, ResearchNodeState>(), Owned(0, 0), ResearchSelection.None);

            Assert.IsTrue(model.Nodes[0].CardState.Locked);
            Assert.AreEqual(LocalizationKeys.Ui.Research.StateUnavailable, model.Nodes[0].StateLabelKey);
            Assert.IsFalse(model.TryGetInitialFocus(out _));
            Assert.IsFalse(model.TryGetDetail(out _));
        }

        [Test]
        public void 研究可能なノードの座標が注目点になり選択の印が付く()
        {
            var states = new Dictionary<Guid, ResearchNodeState> { { Root, ResearchNodeState.Completed }, { Child, ResearchNodeState.Researchable } };
            var model = ResearchTreeViewModelFactory.Create(Definitions(), states, Owned(5, 0), ResearchSelection.Of(Child));

            Assert.IsTrue(model.TryGetInitialFocus(out var focus));
            Assert.AreEqual(new Vector2(200f, 0f), focus);
            Assert.IsFalse(model.Nodes[0].Selected);
            Assert.IsTrue(model.Nodes[1].Selected);
            Assert.IsTrue(model.Nodes[1].CardState.Ready);
        }

        [Test]
        public void 詳細は割当と不足を消費アイテムごとに持ち完了なら不足を出さない()
        {
            var lackingStates = new Dictionary<Guid, ResearchNodeState> { { Child, ResearchNodeState.UnresearchableNotEnoughItem } };
            var lacking = Detail(ResearchTreeViewModelFactory.Create(Definitions(), lackingStates, Owned(1, 1), ResearchSelection.Of(Child)));
            var completedStates = new Dictionary<Guid, ResearchNodeState> { { Child, ResearchNodeState.Completed } };
            var completed = Detail(ResearchTreeViewModelFactory.Create(Definitions(), completedStates, Owned(1, 1), ResearchSelection.Of(Child)));

            // 必要3に対し2個で不足
            // 2 of 3 required is short
            Assert.AreEqual(3, lacking.ConsumeSlots[0].Required);
            Assert.AreEqual(2, lacking.ConsumeSlots[0].Allocated);
            Assert.IsTrue(lacking.ConsumeSlots[0].Lacking);
            Assert.IsFalse(lacking.Button.Interactable);
            Assert.IsFalse(completed.ConsumeSlots[0].Lacking);
            Assert.IsTrue(completed.Button.Completed);
        }

        #region Helpers

        private static ResearchDetailViewModel Detail(ResearchTreeViewModel model)
        {
            Assert.IsTrue(model.TryGetDetail(out var detail));
            return detail;
        }

        private static IReadOnlyDictionary<ItemId, int> Owned(int ore, int oreLevel2)
        {
            return new Dictionary<ItemId, int> { { Ore, ore }, { OreLevel2, oreLevel2 } };
        }

        private static IReadOnlyList<ResearchNodeDefinition> Definitions()
        {
            var noUnlocks = new ResearchUnlockSet(new List<UnlockBlock>(), new List<UnlockMachineRecipe>(), new List<ItemId>(), new List<RewardItem>(), new List<Guid>(), new List<Guid>(), new List<int>(), new List<LevelInheritanceCraftCap>());
            var consume = new List<ResearchConsumeItem> { new(Ore, 3, new List<ItemId> { Ore, OreLevel2 }) };
            return new List<ResearchNodeDefinition>
            {
                new(Root, Ore, Vector2.zero, new List<Guid>(), new List<ResearchConsumeItem>(), noUnlocks),
                new(Child, Ore, new Vector2(200f, 0f), new List<Guid> { Root }, consume, noUnlocks),
            };
        }

        #endregion
    }
}
