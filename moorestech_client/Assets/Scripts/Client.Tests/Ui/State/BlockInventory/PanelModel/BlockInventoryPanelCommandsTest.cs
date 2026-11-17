using Client.Game.InGame.UI.UIState;
using Client.Tests.Ui.State.Toast;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.Commands;
using Client.Ui.State.Routing;
using NUnit.Framework;

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
            var toaster = new RecordingUiCommandResultToaster();
            new BlockInventoryPanelCommands(requester, toaster).Close();
            CollectionAssert.AreEqual(new[] { UIStateEnum.GameScreen }, requester.Requested);
            Assert.AreEqual(1, toaster.Reports.Count);
            Assert.AreEqual(UiStateTransitionRules.RequestCommandName, toaster.Reports[0].CommandName);
            Assert.AreSame(UiCommandAccepted.Instance, toaster.Reports[0].Result);
        }

        [Test]
        public void 口が拒んだらビルドメニューと共有の非良性の閉じ拒否をトーストへ渡す()
        {
            // 開いているのに閉じられないのでトーストで知らせる（ビルドメニューの × と同じ扱い）
            // Open yet not closable, so it must toast, the same as the build menu's close
            var requester = new FakeRequester { Result = false };
            var toaster = new RecordingUiCommandResultToaster();
            new BlockInventoryPanelCommands(requester, toaster).Close();
            Assert.AreEqual(1, toaster.Reports.Count);
            Assert.AreEqual(UiStateTransitionRules.RequestCommandName, toaster.Reports[0].CommandName);
            var refused = (UiCommandRejected)toaster.Reports[0].Result;
            Assert.AreEqual(UiCommandRejectReason.ScreenCloseRefused, refused.Reason);
            Assert.AreEqual("transition_not_allowed", refused.ToWireCode());
            Assert.IsFalse(UiCommandRejectReasonTable.Get(refused.Reason).IsBenign);
        }
    }
}
