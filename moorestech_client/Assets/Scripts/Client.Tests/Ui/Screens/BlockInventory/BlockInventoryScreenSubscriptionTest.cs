using System;
using Client.Game.InGame.UI.UIState;
using Client.Tests.Ui.Screens.GrabOverlay;
using Client.Tests.Ui.Screens.Inventory.Support;
using Client.Tests.Ui.State.BlockInventory.PanelModel;
using Client.Tests.Ui.TestDoubles;
using Client.Ui.Foundation.Input.OsCursor;
using Client.Ui.Foundation.Stage.Columns;
using Client.Ui.Screens.BlockInventory;
using Client.Ui.Screens.BlockInventory.Registry;
using Client.Ui.Screens.BlockInventory.SectionStack;
using Client.Ui.State.BlockInventory.Panel;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UniRx;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BlockInventory
{
    /// <summary>
    /// ブロックのパネルの購読が画面ルータの SubInventory の間だけであることを本物の Presenter で確かめる（§4.2 の ①。前例 InventoryScreenVisibilityTest・PresenterMountTest）
    /// Verifies with the real presenter that the block panel is followed only while the router is on SubInventory (§4.2 type 1; precedents InventoryScreenVisibilityTest, PresenterMountTest)
    /// </summary>
    public class BlockInventoryScreenSubscriptionTest : ScreenTestHost
    {
        private GameObject _host;
        private UIStateControl _control;
        private ReactiveProperty<BlockInventoryPanelState> _state;
        private BlockInventoryView _view;

        [SetUp]
        public void MountPresenter()
        {
            // PresenterMountTest の MountBlockInventory と同じ部品で、チェストを開いた源を渡す
            // Same parts as PresenterMountTest.MountBlockInventory, feeding an opened-chest source
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            _host = new GameObject("block-inventory-subscription");
            var routing = UiRoutingStateTestFactory.CreateSwitchable(_host, UIStateEnum.SubInventory, new[] { UIStateEnum.GameScreen, UIStateEnum.SubInventory }, out _control);
            var columns = new StageColumnsLayout();
            rootVisualElement.Add(columns);
            _state = new ReactiveProperty<BlockInventoryPanelState>(BlockInventoryTestPanels.Chest());
            var source = new FakePanelSource(_state);
            var layout = new BlockInventoryPanelLayoutModel(source, Array.Empty<IBlockInventoryLargePanelRule>());
            layout.Initialize();
            var standard = new SectionStackBodyFactory(BlockInventoryTestBodies.Grids(), new SectionStackSlotRegistry(Array.Empty<ISectionStackSectionFactory>()), source);
            var parts = new BlockInventoryPartRegistry(Array.Empty<IBlockInventoryBodyFactory>(), Array.Empty<IBlockInventoryTitleActionFactory>(), Array.Empty<ITrainInventoryBodyFactory>(), standard);
            new BlockInventoryScreenPresenter(columns, routing, layout, parts, new RecordingPanelCommands(), Toaster, new UiOsCursorRequestStack(new FakeUiOsCursorApplier(), new FakeUiScreenPointer(1f))).Initialize();
            _view = columns.Q<BlockInventoryView>();
            simulate.FrameUpdate();
        }

        [TearDown]
        public void DestroyHost()
        {
            UnityEngine.Object.DestroyImmediate(_host);
        }

        [Test]
        public void 隠れている間にModelが変わっても描き直さない()
        {
            // 画面ルータが SubInventory を離れた後のパネルの変化（開けなかった）を描かない
            // A panel change (open failed) after the router leaves SubInventory is not drawn
            UiRoutingStateTestFactory.Transit(_control, UIStateEnum.GameScreen);
            _state.Value = BlockInventoryTestPanels.OpenFailedChest();
            simulate.FrameUpdate();

            Assert.IsFalse(IsShown(_view));
            // エラーの行は描いたときだけ作られるので、無いか隠れていれば描いていない
            // The error row exists only once drawn, so absent or hidden means not drawn
            var error = _view.Q<Label>("block-inventory-error");
            Assert.IsTrue(error == null || error.resolvedStyle.display == DisplayStyle.None, "隠れている間にエラーを描いた");
        }

        [Test]
        public void 出たら最新の値で1回描く()
        {
            // 隠れている間の変化は、SubInventory へ戻った直後に最新の値で描かれる
            // Changes made while hidden are drawn with the latest value right after returning to SubInventory
            UiRoutingStateTestFactory.Transit(_control, UIStateEnum.GameScreen);
            _state.Value = BlockInventoryTestPanels.OpenFailedChest();
            UiRoutingStateTestFactory.Transit(_control, UIStateEnum.SubInventory);
            simulate.FrameUpdate();

            Assert.IsTrue(IsShown(_view));
            Assert.AreEqual(DisplayStyle.Flex, Find<Label>(_view, "block-inventory-error").resolvedStyle.display, "出しても最新の値で描き直さない");
        }
    }
}
