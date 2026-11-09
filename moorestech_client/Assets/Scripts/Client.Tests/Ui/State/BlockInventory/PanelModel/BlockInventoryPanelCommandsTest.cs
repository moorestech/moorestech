using Client.Game.InGame.UI.UIState;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.Routing;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.Ui.State.BlockInventory.PanelModel
{
    public class BlockInventoryPanelCommandsTest
    {
        private sealed class FakeRequester : IUiStateTransitionRequester
        {
            public readonly System.Collections.Generic.List<UIStateEnum> Requested = new();
            public bool Result = true;

            public bool RequestTransition(UIStateEnum requested)
            {
                Requested.Add(requested);
                return Result;
            }
        }

        [Test]
        public void Closeは遷移要求の口へGameScreenを1回渡す()
        {
            var requester = new FakeRequester();
            new BlockInventoryPanelCommands(requester).Close();
            CollectionAssert.AreEqual(new[] { UIStateEnum.GameScreen }, requester.Requested);
        }

        [Test]
        public void 口が拒んだらWarningログを出す()
        {
            var requester = new FakeRequester { Result = false };
            LogAssert.Expect(LogType.Warning, "[BlockInventoryPanelCommands] close refused by UiStateTransitionRules");
            new BlockInventoryPanelCommands(requester).Close();
        }
    }
}
