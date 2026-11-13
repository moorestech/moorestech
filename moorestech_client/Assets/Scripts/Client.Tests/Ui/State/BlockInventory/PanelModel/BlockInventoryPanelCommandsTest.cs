using Client.Game.InGame.UI.UIState;
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
            var result = new BlockInventoryPanelCommands(requester).Close();
            CollectionAssert.AreEqual(new[] { UIStateEnum.GameScreen }, requester.Requested);
            Assert.AreSame(UiCommandAccepted.Instance, result);
        }

        [Test]
        public void 口が拒んだらビルドメニューと共有の非良性の閉じ拒否を返す()
        {
            // 開いているのに閉じられないのでトーストで知らせる（ビルドメニューの × と同じ扱い）
            // Open yet not closable, so it must toast, the same as the build menu's close
            var requester = new FakeRequester { Result = false };
            var refused = (UiCommandRejected)new BlockInventoryPanelCommands(requester).Close();
            Assert.AreEqual(UiCommandRejectReason.ScreenCloseRefused, refused.Reason);
            Assert.AreEqual("transition_not_allowed", refused.ToWireCode());
            Assert.IsFalse(UiCommandRejectReasonTraits.IsBenign(refused.Reason));
        }
    }
}
