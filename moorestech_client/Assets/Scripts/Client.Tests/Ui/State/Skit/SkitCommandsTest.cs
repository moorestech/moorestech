using Client.Skit.Skit;
using Client.Skit.UI;
using Client.Tests.Ui.Screens;
using Client.Tests.Ui.State.Toast;
using Client.Ui.State.Commands;
using Client.Ui.State.Skit;
using Client.Ui.State.Toast;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.Ui.State.Skit
{
    /// <summary>
    /// 会話の操作が store の検証を通り、失敗は会話の拒否としてトーストに出ることを確かめる（L1。言語固定のため ScreenTestHost）
    /// Verifies that skit operations go through the store's validation and failures surface as a skit rejection toast (L1; ScreenTestHost pins the language)
    /// </summary>
    public class SkitCommandsTest : ScreenTestHost
    {
        private SkitPresentationStateStore _store;
        private RecordingUiCommandResultToaster _recorded;
        private SkitCommands _commands;

        [SetUp]
        public void PresentBlockingText()
        {
            _store = new SkitPresentationStateStore();
            _store.BeginBlocking(new SkitActionContext());
            _store.PresentBlockingText("Moore", "Blocking message");
            _recorded = new RecordingUiCommandResultToaster();
            _commands = new SkitCommands(_store, _recorded);
        }

        [Test]
        public void 受け付けた操作は受理を返す()
        {
            var current = _store.GetCurrent();
            _commands.SetAuto(current.SessionId, current.SceneRevision, true);
            Assert.AreEqual(SkitCommands.SetAutoCommandName, _recorded.Reports[0].CommandName);
            Assert.IsInstanceOf<UiCommandAccepted>(_recorded.Reports[0].Result);
            Assert.IsTrue(_store.GetCurrent().PresentationState.AutoEnabled);
        }

        [Test]
        public void 古い版の操作は会話の拒否で返り失敗コードはstoreの理由になる()
        {
            var current = _store.GetCurrent();
            _commands.Advance(current.SessionId, current.SceneRevision - 1);

            Assert.AreEqual(SkitCommands.AdvanceCommandName, _recorded.Reports[0].CommandName);
            var rejected = (UiCommandRejected)_recorded.Reports[0].Result;
            Assert.AreEqual(UiCommandRejectReason.SkitIntentRejected, rejected.Reason);
            Assert.AreEqual("stale_revision", rejected.ToWireCode());
        }

        [Test]
        public void 会話の拒否は警告とトーストで知らせる()
        {
            var current = _store.GetCurrent();
            LogAssert.Expect(LogType.Warning, "[UiCommandResultToaster] skit.select failed: intent_not_allowed");
            new SkitCommands(_store, Toaster).Select(current.SessionId, current.SceneRevision, "route-a");

            Assert.IsFalse(UiCommandRejectReasonTable.Get(UiCommandRejectReason.SkitIntentRejected).IsBenign);
            Assert.AreEqual(1, ShownToasts.Count);
            Assert.AreEqual("The conversation did not accept that operation", ShownToasts[0].Text);
            Assert.AreEqual(GameUiToastVariant.Error, ShownToasts[0].Variant);
        }
    }
}
