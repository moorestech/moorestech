using System;
using System.Collections.Generic;
using Client.Game.InGame.UI.UIState;
using Client.Tests.Ui.Screens.GrabOverlay;
using Client.Tests.Ui.Screens.Inventory.Support;
using Client.Tests.Ui.State.Inventory;
using Client.Tests.Ui.TestDoubles;
using Client.Ui.Foundation.Input.OsCursor;
using Client.Ui.Screens.Research;
using Client.Ui.State.Research;
using Client.Ui.State.Research.Catalog;
using Client.Ui.State.Research.Tree;
using Game.Research;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using Kit = Client.Tests.Ui.Screens.Research.ResearchScreenTestKit;

namespace Client.Tests.Ui.Screens.Research
{
    /// <summary>
    /// 研究画面の購読が研究画面の間だけで、出たら最新の値で描くことを本物の Model と Presenter で確かめる（§4.2 の ①。前例 BlockInventoryScreenSubscriptionTest・ResearchTreeModelTest）
    /// Verifies with the real model and presenter that the research screen follows only while shown and draws the latest value on show (§4.2 type 1; precedents BlockInventoryScreenSubscriptionTest, ResearchTreeModelTest)
    /// </summary>
    public class ResearchTreeSubscriptionTest : ScreenTestHost
    {
        private GameObject _host;
        private Texture2D _texture;
        private UIStateControl _control;
        private ResearchNodeCatalog _catalog;
        private ResearchNodeStateStore _store;

        [SetUp]
        public void MountPresenter()
        {
            // ResearchTreeModelTest と同じ組み立て。状態の Store は Initialize せず反映の口だけ使う
            // Same assembly as ResearchTreeModelTest; the state store is used without Initialize, only its apply port
            _texture = new Texture2D(4, 4) { hideFlags = HideFlags.DontSave };
            var content = Kit.LoadMasterAndContent(_texture);
            _host = new GameObject("research-subscription");
            var routing = UiRoutingStateTestFactory.CreateSwitchable(_host, UIStateEnum.PlayerInventory, new[] { UIStateEnum.PlayerInventory, UIStateEnum.ResearchTree }, out _control);
            _catalog = new ResearchNodeCatalog();
            _store = new ResearchNodeStateStore(null, null);
            var selection = new ResearchSelectionStore(routing);
            selection.Initialize();

            // 本番の登録と同じく Model を先に Initialize し、ルータの通知で Model が Presenter より先に組み直す
            // As in production registration, initialize the model first so it rebuilds before the presenter on router notifications
            var model = new ResearchTreeModel(_catalog, _store, selection, new RecordingPlayerInventoryOperations(9, 2), routing);
            model.Initialize();
            var root = GameUiRootTestFactory.Create(_host, rootVisualElement);
            new ResearchTreePresenter(root, model, routing, new RecordingResearchTreeCommands(), content, new UiOsCursorRequestStack(new FakeUiOsCursorApplier(), new FakeUiScreenPointer(1f))).Initialize();
            simulate.FrameUpdate();
        }

        [TearDown]
        public void DestroyHost()
        {
            UnityEngine.Object.DestroyImmediate(_host);
            UnityEngine.Object.DestroyImmediate(_texture);
        }

        [Test]
        public void 出たら最新の値で1回描く()
        {
            // 未完了で描いてから隠し、隠れている間に届いた完了の状態が戻った直後に描かれる
            // Drawn as not completed then hidden; the completed states received while hidden are drawn right after returning
            var target = _catalog.Nodes[0].Guid;
            UiRoutingStateTestFactory.Transit(_control, UIStateEnum.ResearchTree);
            simulate.FrameUpdate();
            Assert.IsFalse(Card(target).ClassListContains("research-tree__card--completed"), "前提: 最初は未完了で描く");

            UiRoutingStateTestFactory.Transit(_control, UIStateEnum.PlayerInventory);
            _store.ApplyNodeStates(AllStates(ResearchNodeState.Completed));
            UiRoutingStateTestFactory.Transit(_control, UIStateEnum.ResearchTree);
            simulate.FrameUpdate();

            Assert.IsTrue(IsShown(rootVisualElement.Q<ResearchTreeView>()));
            Assert.IsTrue(Card(target).ClassListContains("research-tree__card--completed"), "出しても隠れている間に届いた状態で描き直さない");
        }

        private VisualElement Card(Guid guid)
        {
            return Find<VisualElement>(rootVisualElement, $"research-node-{guid}");
        }

        private Dictionary<Guid, ResearchNodeState> AllStates(ResearchNodeState state)
        {
            var states = new Dictionary<Guid, ResearchNodeState>();
            foreach (var node in _catalog.Nodes) states[node.Guid] = state;
            return states;
        }
    }
}
