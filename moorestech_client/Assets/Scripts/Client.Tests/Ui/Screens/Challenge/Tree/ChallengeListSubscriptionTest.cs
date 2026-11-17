using Client.Tests.Ui.Screens.GrabOverlay;
using Client.Game.InGame.UI.UIState;
using Client.Tests.Ui.Screens.Inventory.Support;
using Client.Tests.Ui.TestDoubles;
using Client.Ui.Foundation.Input.OsCursor;
using Client.Ui.Screens.Challenge.Tree;
using Client.Ui.State.Challenge.Tree;
using Client.Ui.State.Challenge.Tree.Selection;
using NUnit.Framework;
using UnityEngine;
using Kit = Client.Tests.Ui.Screens.Challenge.Tree.ChallengeListScreenTestKit;
using Source = Client.Tests.Ui.State.Challenge.ChallengeProgressStateTestSource;

namespace Client.Tests.Ui.Screens.Challenge.Tree
{
    /// <summary>
    /// チャレンジ一覧の購読が一覧画面の間だけで、出たら最新の値で描くことを本物の Model と Presenter で確かめる（§4.2 の ①。前例 ResearchTreeSubscriptionTest）
    /// Verifies with the real model and presenter that the challenge list follows only while shown and draws the latest value on show (§4.2 type 1; precedent ResearchTreeSubscriptionTest)
    /// </summary>
    public class ChallengeListSubscriptionTest : ScreenTestHost
    {
        private GameObject _host;
        private UIStateControl _control;
        private ChallengeCategorySelectionStore _selection;

        [SetUp]
        public void MountPresenter()
        {
            // 本番の登録と同じく選択ストア・Model を先に Initialize し、ルータの通知で Model が Presenter より先に組み直す
            // As in production registration, initialize the selection store and model first so the model rebuilds before the presenter on router notifications
            Kit.LoadMasterAndDictionary();
            _host = new GameObject("challenge-list-subscription");
            var routing = UiRoutingStateTestFactory.CreateSwitchable(_host, UIStateEnum.GameScreen, new[] { UIStateEnum.GameScreen, UIStateEnum.ChallengeList }, out _control);
            var source = new Source();
            _selection = new ChallengeCategorySelectionStore(routing);
            _selection.Initialize();
            var model = new ChallengeListModel(source.Progress, _selection, routing);
            model.Initialize();
            var root = GameUiRootTestFactory.Create(_host, rootVisualElement);
            new ChallengeListPresenter(root, model, routing, new RecordingChallengeListCommands(), new UiOsCursorRequestStack(new FakeUiOsCursorApplier(), new FakeUiScreenPointer(1f))).Initialize();
            simulate.FrameUpdate();
        }

        [TearDown]
        public void DestroyHost()
        {
            UnityEngine.Object.DestroyImmediate(_host);
        }

        [Test]
        public void 出たら最新の値で描く()
        {
            // 開いて選択を替え、離れて開き直すと選択が先頭へ戻った最新の値で描かれる
            // Open and change the selection, then leave and reopen: the latest value with the selection back to the first is drawn
            Assert.IsFalse(IsShown(rootVisualElement.Q<ChallengeListView>()), "前提: 一覧の外では隠れている");
            UiRoutingStateTestFactory.Transit(_control, UIStateEnum.ChallengeList);
            simulate.FrameUpdate();
            Assert.IsTrue(IsShown(rootVisualElement.Q<ChallengeListView>()));
            _selection.Select(Source.Category2);
            simulate.FrameUpdate();
            Assert.IsNotNull(Kit.Card(rootVisualElement, Source.Category2First), "前提: 選んだカテゴリを描く");

            UiRoutingStateTestFactory.Transit(_control, UIStateEnum.GameScreen);
            simulate.FrameUpdate();
            Assert.IsFalse(IsShown(rootVisualElement.Q<ChallengeListView>()));
            UiRoutingStateTestFactory.Transit(_control, UIStateEnum.ChallengeList);
            simulate.FrameUpdate();

            Assert.IsTrue(IsShown(rootVisualElement.Q<ChallengeListView>()));
            Assert.IsNotNull(Kit.Card(rootVisualElement, Source.Test1), "開き直すと先頭のカテゴリの最新の値で描く");
        }
    }
}
