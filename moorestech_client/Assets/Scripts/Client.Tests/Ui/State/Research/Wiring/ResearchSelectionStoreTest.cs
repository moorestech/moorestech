using System;
using Client.Game.InGame.UI.UIState;
using Client.Tests.Ui.Screens.Inventory.Support;
using Client.Ui.State.Research.Tree;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Client.Tests.Ui.State.Research.Wiring
{
    /// <summary>
    /// 研究画面を離れたら選択を捨て、画面の中では保つ（L0）
    /// Drops the selection on leaving the research screen and keeps it inside (L0)
    /// </summary>
    public class ResearchSelectionStoreTest
    {
        private static readonly Guid Node = Guid.Parse("00000000-0000-0000-0000-0000000000b1");
        private GameObject _host;

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("ResearchSelectionStoreTest");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_host);
        }

        [Test]
        public void 研究画面から離れると選択が無くなる()
        {
            var routing = UiRoutingStateTestFactory.CreateSwitchable(_host, UIStateEnum.ResearchTree, new[] { UIStateEnum.ResearchTree, UIStateEnum.PlayerInventory }, out var control);
            var selection = new ResearchSelectionStore(routing);
            selection.Initialize();

            selection.Toggle(Node);
            Assert.IsTrue(selection.Current.Value.Is(Node), "研究画面の中では選択を保つ");

            UiRoutingStateTestFactory.Transit(control, UIStateEnum.PlayerInventory);
            Assert.AreSame(ResearchSelection.None, selection.Current.Value);
        }
    }
}
