using System;
using System.Linq;
using Client.Network.API;
using Client.Ui.State.Challenge.Tree;
using Client.Ui.State.Challenge.Tree.Selection;
using Core.Master;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Client.Tests.Ui.State.Challenge
{
    /// <summary>
    /// チャレンジの応答から一覧の表示を組む規則（L0。テスト mod の challenges.json）
    /// Rules that build the list display from challenge responses (L0; test mod challenges.json)
    /// </summary>
    public class ChallengeListViewModelFactoryTest
    {
        private static readonly Guid Category1 = Guid.Parse("03ca4ded-3b2b-4e7f-bb6e-430f060c4ed1");
        private static readonly Guid Category2 = Guid.Parse("35330f9d-f44f-493d-a6bc-07ae6413d7c4");
        private static readonly Guid Test1 = Guid.Parse("00000000-0000-0000-4567-000000000001");
        private static readonly Guid Test2 = Guid.Parse("00000000-0000-0000-4567-000000000002");
        private static readonly Guid Test3 = Guid.Parse("00000000-0000-0000-4567-000000000003");
        private static readonly Guid Test4 = Guid.Parse("00000000-0000-0000-4567-000000000004");
        private static readonly Guid Research1 = Guid.Parse("00000000-0000-0000-4567-000000000101");
        private static readonly Guid Category2First = Guid.Parse("c2d84cfb-73cb-4ffa-99b5-9703c550f330");

        [SetUp]
        public void LoadMaster()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [Test]
        public void 未解放のカテゴリを除きマスタの順で状態と座標と前提を写す()
        {
            var model = AsSelected(ChallengeListViewModelFactory.Create(new[] { Response(Category1, true, new[] { Test2 }, new[] { Test1 }), Response(Category2, false, new Guid[0], new Guid[0]) }, ChallengeCategorySelection.None));

            // 未解放の Category2 は出ない。ノードはマスタの順
            // Locked Category2 is absent; nodes follow master order
            Assert.AreEqual(1, model.Categories.Count);
            var nodes = model.Categories[0].Nodes;
            var master = MasterHolder.ChallengeMaster.GetChallengeCategory(Category1).Challenges;
            CollectionAssert.AreEqual(master.Select(c => c.ChallengeGuid).ToList(), nodes.Select(n => n.Guid).ToList());

            // 完了・進行中・未解放と、その文言キー（Web resolveChallengeStateKey）
            // Completed, current, locked and their label keys (Web resolveChallengeStateKey)
            Assert.AreEqual(ChallengeNodeState.Completed, nodes.Single(n => n.Guid == Test1).State);
            Assert.AreEqual(ChallengeNodeState.Current, nodes.Single(n => n.Guid == Test2).State);
            Assert.AreEqual(ChallengeNodeState.Locked, nodes.Single(n => n.Guid == Test3).State);
            Assert.AreEqual(LocalizationKeys.Ui.Challenge.StateCompleted.Key, nodes.Single(n => n.Guid == Test1).StateLabelKey.Key);
            Assert.AreEqual(LocalizationKeys.Ui.Challenge.StateCurrent.Key, nodes.Single(n => n.Guid == Test2).StateLabelKey.Key);
            Assert.AreEqual(LocalizationKeys.Ui.Challenge.StateLocked.Key, nodes.Single(n => n.Guid == Test3).StateLabelKey.Key);

            // 座標と倍率はマスタの displayListParam、前提はマスタの prevChallengeGuids
            // Position and scale come from the master displayListParam; prerequisites from prevChallengeGuids
            var research1 = master.Single(c => c.ChallengeGuid == Research1).DisplayListParam;
            Assert.AreEqual(new Vector2(research1.UIPosition.x, research1.UIPosition.y), nodes.Single(n => n.Guid == Research1).TreePosition);
            Assert.AreEqual(new Vector2(research1.UIScale.x, research1.UIScale.y), nodes.Single(n => n.Guid == Research1).Scale);
            CollectionAssert.AreEqual(new[] { Test1 }, nodes.Single(n => n.Guid == Test4).PreviousGuids);

            // 注目点は進行中の座標
            // The focus is the current node position
            Assert.IsTrue(model.Categories[0].TryGetInitialFocus(out var focus));
            Assert.AreEqual(nodes.Single(n => n.Guid == Test2).TreePosition, focus);
        }

        [Test]
        public void 選択が無いか出ていないカテゴリなら先頭を選び出ていればそれを選ぶ()
        {
            var both = new[] { Response(Category1, true, new[] { Test2 }, new[] { Test1 }), Response(Category2, true, new[] { Category2First }, new Guid[0]) };
            Assert.AreEqual(Category1, Selected(ChallengeListViewModelFactory.Create(both, ChallengeCategorySelection.None)));
            Assert.AreEqual(Category2, Selected(ChallengeListViewModelFactory.Create(both, ChallengeCategorySelection.Of(Category2))));
            Assert.AreEqual(Category1, Selected(ChallengeListViewModelFactory.Create(both, ChallengeCategorySelection.Of(Guid.NewGuid()))));

            // 選んだカテゴリが未解放なら出ないので先頭、解放済みが無ければ空の一覧（選択を持たない）
            // A selected but locked category is absent so the first is used; with no unlocked categories the list is empty (no selection)
            var lockedSecond = new[] { both[0], Response(Category2, false, new Guid[0], new Guid[0]) };
            Assert.AreEqual(Category1, Selected(ChallengeListViewModelFactory.Create(lockedSecond, ChallengeCategorySelection.Of(Category2))));
            var none = ChallengeListViewModelFactory.Create(new[] { Response(Category1, false, new Guid[0], new Guid[0]) }, ChallengeCategorySelection.Of(Category1));
            Assert.IsInstanceOf<EmptyChallengeListViewModel>(none);
            Assert.AreSame(ChallengeListViewModel.Empty, none);
        }

        [Test]
        public void 別カテゴリの前提は線のキーから除く()
        {
            var foreign = Guid.Parse("00000000-0000-0000-0000-00000000f00d");
            var inside = new ChallengeNodeCardViewModel(Test1, Vector2.zero, Vector2.one, new Guid[0], ChallengeNodeState.Completed);
            var crossing = new ChallengeNodeCardViewModel(Test2, new Vector2(220f, 0f), Vector2.one, new[] { Test1, foreign }, ChallengeNodeState.Current);
            var category = ChallengeCategoryViewModel.Create(Category1, new[] { inside, crossing });
            CollectionAssert.AreEqual(new[] { Test1 }, category.Nodes[1].PreviousGuids);
            CollectionAssert.IsEmpty(category.Nodes[0].PreviousGuids);
        }

        [Test]
        public void グラフの形は並びと座標と前提と注目点で比べ状態と倍率では変わらない()
        {
            var first = Card(Test1, Vector2.zero, Vector2.one, ChallengeNodeState.Completed);
            var second = Card(Test2, new Vector2(220f, 0f), Vector2.one, ChallengeNodeState.Current, Test1);
            var shown = Category(first, second);

            // 倍率だけの違いはカードの描き直しで足り、同じ形
            // A scale-only difference needs just a card redraw, so the shape is the same
            Assert.IsTrue(shown.HasSameGraphAs(Category(first, Card(Test2, new Vector2(220f, 0f), new Vector2(2f, 2f), ChallengeNodeState.Current, Test1))));

            // 座標・前提・並び・注目点のどれかが変われば違う形
            // A change in position, prerequisites, order or focus is a different shape
            Assert.IsFalse(shown.HasSameGraphAs(Category(first, Card(Test2, new Vector2(240f, 0f), Vector2.one, ChallengeNodeState.Current, Test1))), "座標");
            Assert.IsFalse(shown.HasSameGraphAs(Category(first, Card(Test2, new Vector2(220f, 0f), Vector2.one, ChallengeNodeState.Current))), "前提");
            Assert.IsFalse(shown.HasSameGraphAs(Category(second, first)), "並び");
            Assert.IsFalse(shown.HasSameGraphAs(Category(first, Card(Test2, new Vector2(220f, 0f), Vector2.one, ChallengeNodeState.Completed, Test1))), "注目点");
        }

        private static ChallengeNodeCardViewModel Card(Guid guid, Vector2 position, Vector2 scale, ChallengeNodeState state, params Guid[] previous)
        {
            return new ChallengeNodeCardViewModel(guid, position, scale, previous, state);
        }

        private static ChallengeCategoryViewModel Category(params ChallengeNodeCardViewModel[] nodes)
        {
            return ChallengeCategoryViewModel.Create(Category1, nodes);
        }

        private static ChallengeCategoryResponse Response(Guid category, bool unlocked, Guid[] current, Guid[] completed)
        {
            var master = MasterHolder.ChallengeMaster;
            return new ChallengeCategoryResponse(master.GetChallengeCategory(category), unlocked, current.Select(master.GetChallenge).ToList(), completed.Select(master.GetChallenge).ToList());
        }

        private static SelectedChallengeListViewModel AsSelected(ChallengeListViewModel model)
        {
            Assert.IsInstanceOf<SelectedChallengeListViewModel>(model);
            return (SelectedChallengeListViewModel)model;
        }

        private static Guid Selected(ChallengeListViewModel model)
        {
            return AsSelected(model).Selected.Guid;
        }
    }
}
