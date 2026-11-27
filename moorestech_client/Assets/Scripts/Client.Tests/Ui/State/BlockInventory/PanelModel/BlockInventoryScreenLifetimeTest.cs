using Client.Game.InGame.UI.UIState;
using Client.Tests.Ui.Screens.Inventory.Support;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.BlockInventory.Panel.Lifetime;
using NUnit.Framework;
using UniRx;
using UnityEngine;

namespace Client.Tests.Ui.State.BlockInventory.PanelModel
{
    /// <summary>
    /// 機械の画面が出ている間だけ生きるトークンを、実際の画面遷移とパネルの状態で確かめる（L0）
    /// Checks the token alive only while the machine screen is shown, through real screen transitions and panel states (L0)
    /// </summary>
    public class BlockInventoryScreenLifetimeTest
    {
        private GameObject _host;
        private UIStateControl _control;
        private FakePanelSource _panel;
        private BlockInventoryScreenLifetime _lifetime;

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("BlockInventoryScreenLifetimeTest");
            var routing = UiRoutingStateTestFactory.CreateSwitchable(_host, UIStateEnum.GameScreen, new[] { UIStateEnum.GameScreen, UIStateEnum.SubInventory }, out _control);
            _panel = new FakePanelSource(new ReactiveProperty<BlockInventoryPanelState>(BlockInventoryPanelState.Closed));
            _lifetime = new BlockInventoryScreenLifetime(routing, _panel);
            _lifetime.Initialize();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_host);
        }

        [Test]
        public void 画面が出ていない間のトークンは取り消し済み()
        {
            Assert.IsTrue(_lifetime.ShownToken.IsCancellationRequested);
        }

        [Test]
        public void 画面が出ると生きたトークンになり_画面を離れると取り消される()
        {
            OpenMachineScreen();
            var shown = _lifetime.ShownToken;
            Assert.IsFalse(shown.IsCancellationRequested);

            UiRoutingStateTestFactory.Transit(_control, UIStateEnum.GameScreen);

            Assert.IsTrue(shown.IsCancellationRequested);
            Assert.IsTrue(_lifetime.ShownToken.IsCancellationRequested);
        }

        [Test]
        public void 画面ルータがSubInventoryのままでもパネルが閉じれば取り消される()
        {
            // 機械が撤去された直後など、画面遷移より先にパネルだけ閉になる
            // Right after the machine is removed, the panel closes before the screen transition
            OpenMachineScreen();
            var shown = _lifetime.ShownToken;

            _panel.State.Value = BlockInventoryPanelState.Closed;

            Assert.IsTrue(shown.IsCancellationRequested);
        }

        [Test]
        public void 出ている間のパネルの中身の更新では取り消されない()
        {
            OpenMachineScreen();
            var shown = _lifetime.ShownToken;

            _panel.State.Value = BlockInventoryTestPanels.OfType("Machine");

            Assert.IsFalse(shown.IsCancellationRequested);
        }

        [Test]
        public void 開き直すと前の寿命とは別の生きたトークンになる()
        {
            OpenMachineScreen();
            var first = _lifetime.ShownToken;
            UiRoutingStateTestFactory.Transit(_control, UIStateEnum.GameScreen);
            _panel.State.Value = BlockInventoryPanelState.Closed;

            OpenMachineScreen();

            Assert.IsTrue(first.IsCancellationRequested);
            Assert.IsFalse(_lifetime.ShownToken.IsCancellationRequested);
        }

        private void OpenMachineScreen()
        {
            UiRoutingStateTestFactory.Transit(_control, UIStateEnum.SubInventory);
            _panel.State.Value = BlockInventoryTestPanels.OfType("Chest");
        }
    }
}
