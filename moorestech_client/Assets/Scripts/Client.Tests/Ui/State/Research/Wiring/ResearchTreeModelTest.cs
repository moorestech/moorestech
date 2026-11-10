using System;
using System.Collections.Generic;
using Client.Game.InGame.UI.UIState;
using Client.Tests.Ui.Screens.Inventory.Support;
using Client.Tests.Ui.State.Inventory;
using Client.Ui.State.Research;
using Client.Ui.State.Research.Catalog;
using Client.Ui.State.Research.Tree;
using Game.Research;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Client.Tests.Ui.State.Research.Wiring
{
    /// <summary>
    /// 研究ツリーの表示は研究画面の表示中だけ組み、突入・選択・状態・所持の変化で組み直す（L0）
    /// The research tree display builds only while shown and rebuilds on entry, selection, state and inventory changes (L0)
    /// </summary>
    public class ResearchTreeModelTest
    {
        private GameObject _host;
        private UIStateControl _control;
        private ResearchNodeCatalog _catalog;
        private ResearchNodeStateStore _store;
        private ResearchSelectionStore _selection;
        private RecordingPlayerInventoryOperations _inventory;
        private ResearchTreeModel _model;

        [SetUp]
        public void SetUp()
        {
            // 定義はテスト mod の研究マスタから組む。状態の Store は Initialize せず反映の口だけ使う
            // Definitions come from the test mod research master; the state store is used without Initialize, only its apply port
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            _host = new GameObject("ResearchTreeModelTest");
            var routing = UiRoutingStateTestFactory.CreateSwitchable(_host, UIStateEnum.PlayerInventory, new[] { UIStateEnum.PlayerInventory, UIStateEnum.ResearchTree }, out _control);
            _catalog = new ResearchNodeCatalog();
            _store = new ResearchNodeStateStore(null, null);
            _selection = new ResearchSelectionStore(routing);
            _selection.Initialize();
            _inventory = new RecordingPlayerInventoryOperations(9, 2);
            _model = new ResearchTreeModel(_catalog, _store, _selection, _inventory, routing);
            _model.Initialize();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_host);
        }

        [Test]
        public void 閉画面では状態や所持が変わっても組まない()
        {
            _store.ApplyNodeStates(AllStates(ResearchNodeState.Researchable));
            _inventory.RaiseSnapshotChanged();

            Assert.AreSame(ResearchTreeViewModel.Empty, _model.Current.Value);
        }

        [Test]
        public void 研究画面へ入ると全ノードを組む()
        {
            UiRoutingStateTestFactory.Transit(_control, UIStateEnum.ResearchTree);

            Assert.AreEqual(_catalog.Nodes.Count, _model.Current.Value.Nodes.Count);
            Assert.IsFalse(_model.Current.Value.TryGetDetail(out _));
        }

        [Test]
        public void 表示中は選択と状態と所持の変化で組み直す()
        {
            UiRoutingStateTestFactory.Transit(_control, UIStateEnum.ResearchTree);
            var target = _catalog.Nodes[0].Guid;

            // 選択で詳細が付く
            // Selection adds the detail
            _selection.Toggle(target);
            Assert.IsTrue(_model.Current.Value.TryGetDetail(out var detail));
            Assert.AreEqual(target, detail.Guid);

            // 状態の受信でカードが替わる
            // Receiving states changes the cards
            _store.ApplyNodeStates(AllStates(ResearchNodeState.Completed));
            Assert.IsTrue(_model.Current.Value.Nodes[0].CardState.Completed);

            // 所持の変化で組み直す
            // An inventory change rebuilds
            var beforeInventoryChange = _model.Current.Value;
            _inventory.RaiseSnapshotChanged();
            Assert.AreNotSame(beforeInventoryChange, _model.Current.Value);
        }

        private Dictionary<Guid, ResearchNodeState> AllStates(ResearchNodeState state)
        {
            var states = new Dictionary<Guid, ResearchNodeState>();
            foreach (var node in _catalog.Nodes) states[node.Guid] = state;
            return states;
        }
    }
}
