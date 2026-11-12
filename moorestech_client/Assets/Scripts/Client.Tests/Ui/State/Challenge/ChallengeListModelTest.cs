using System;
using System.Linq;
using Client.Game.InGame.UI.UIState;
using Client.Tests.Ui.Screens.Inventory.Support;
using Client.Ui.State.Challenge.Tree;
using Client.Ui.State.Challenge.Tree.Selection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;
using Object = UnityEngine.Object;
using Source = Client.Tests.Ui.State.Challenge.ChallengeProgressStateTestSource;

namespace Client.Tests.Ui.State.Challenge
{
    /// <summary>
    /// チャレンジ一覧の表示は一覧の表示中だけ組み、突入・選択・完了の知らせで組み直し、離れると先頭のカテゴリへ戻る（L0。前例 ResearchTreeModelTest）
    /// The challenge list display builds only while shown, rebuilds on entry, selection and completion notices, and returns to the first category after leaving (L0; precedent ResearchTreeModelTest)
    /// </summary>
    public class ChallengeListModelTest
    {
        private GameObject _host;
        private UIStateControl _control;
        private Source _source;
        private ChallengeCategorySelectionStore _selection;
        private ChallengeListModel _model;

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_host);
        }

        [Test]
        public void 閉画面では完了の知らせと選択の変化で組まない()
        {
            Build(true);
            _selection.Select(Source.Category2);
            _source.CompleteTest2();

            Assert.AreSame(ChallengeListViewModel.Empty, _model.Current.Value);
        }

        [Test]
        public void 一覧へ入ると先頭のカテゴリで組み表示中は選択と完了の知らせで組み直す()
        {
            // 突入で解放済みの 2 カテゴリを受信順で組み、先頭を選ぶ
            // Entering builds both unlocked categories in received order and selects the first
            Build(true);
            UiRoutingStateTestFactory.Transit(_control, UIStateEnum.ChallengeList);
            var entered = AsSelected(_model.Current.Value);
            CollectionAssert.AreEqual(new[] { Source.Category1, Source.Category2 }, entered.CategoryGuids);
            Assert.AreEqual(Source.Category1, entered.Selected.Guid);

            // 選択の変化で組み直す
            // A selection change rebuilds
            _selection.Select(Source.Category2);
            Assert.AreEqual(Source.Category2, AsSelected(_model.Current.Value).Selected.Guid);

            // 完了の知らせ（Categories の置き換えの後）で組み直し、Test2 が完了になる
            // A completion notice (after Categories is replaced) rebuilds and Test2 becomes completed
            var beforeCompletion = _model.Current.Value;
            _source.CompleteTest2();
            Assert.AreNotSame(beforeCompletion, _model.Current.Value);
            var basics = AsSelected(_model.Current.Value).Categories.Single(c => c.Guid == Source.Category1);
            Assert.AreEqual(ChallengeNodeState.Completed, basics.Nodes.Single(n => n.Guid == Source.Test2).State);
        }

        // 本番の登録順（選択ストア → Model）と逆順の両方で、離れた後の選択の戻しでは組まず、開き直すと先頭のカテゴリ（D-21）
        // In both the production order (selection store, then model) and the reverse, the reset after leaving builds nothing and reopening shows the first category (D-21)
        [TestCase(true)]
        [TestCase(false)]
        public void 一覧を離れると選択が無しに戻り開き直すと先頭のカテゴリを組む(bool selectionFirst)
        {
            Build(selectionFirst);
            UiRoutingStateTestFactory.Transit(_control, UIStateEnum.ChallengeList);
            _selection.Select(Source.Category2);
            var shown = _model.Current.Value;

            // 離れると選択は無しに戻るが、閉じた画面では組まない（最後に出した表示のまま）
            // Leaving resets the selection, but a closed screen builds nothing (the last shown display stays)
            UiRoutingStateTestFactory.Transit(_control, UIStateEnum.GameScreen);
            Assert.AreSame(ChallengeCategorySelection.None, _selection.Current.Value);
            Assert.AreSame(shown, _model.Current.Value);

            // 開き直すと先頭のカテゴリで組む
            // Reopening builds with the first category
            UiRoutingStateTestFactory.Transit(_control, UIStateEnum.ChallengeList);
            Assert.AreEqual(Source.Category1, AsSelected(_model.Current.Value).Selected.Guid);
        }

        private void Build(bool selectionFirst)
        {
            // テスト mod のマスタを読み、ゲーム画面と一覧の間を行き来できる経路で組む
            // Load the test mod master and build on a router that moves between the game screen and the list
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            _host = new GameObject("ChallengeListModelTest");
            var routing = UiRoutingStateTestFactory.CreateSwitchable(_host, UIStateEnum.GameScreen, new[] { UIStateEnum.GameScreen, UIStateEnum.ChallengeList }, out _control);
            _source = new Source();
            _selection = new ChallengeCategorySelectionStore(routing);
            _model = new ChallengeListModel(_source.Progress, _selection, routing);

            // 初期化の順を入れ替えられるようにする（DI の登録順に依らないことを見る）
            // Allow either initialization order (checks independence from DI registration order)
            if (selectionFirst)
            {
                _selection.Initialize();
                _model.Initialize();
                return;
            }
            _model.Initialize();
            _selection.Initialize();
        }

        private static SelectedChallengeListViewModel AsSelected(ChallengeListViewModel model)
        {
            Assert.IsInstanceOf<SelectedChallengeListViewModel>(model);
            return (SelectedChallengeListViewModel)model;
        }
    }
}
