using Client.Skit.Skit;
using Client.Skit.UI;
using Client.Tests.Ui.Screens;
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
        private SkitCommands _commands;

        [SetUp]
        public void PresentBlockingText()
        {
            _store = new SkitPresentationStateStore();
            _store.BeginBlocking(new SkitActionContext());
            _store.PresentBlockingText("Moore", "Blocking message");
            _commands = new SkitCommands(_store);
        }

        [Test]
        public void 受け付けた操作は受理を返す()
        {
            var current = _store.GetCurrent();
            Assert.IsInstanceOf<UiCommandAccepted>(_commands.SetAuto(current.SessionId, current.SceneRevision, true));
            Assert.IsTrue(_store.GetCurrent().PresentationState.AutoEnabled);
        }

        [Test]
        public void 古い版の操作は会話の拒否で返り失敗コードはstoreの理由になる()
        {
            var current = _store.GetCurrent();
            var result = _commands.Advance(current.SessionId, current.SceneRevision - 1);

            var rejected = (UiCommandRejected)result;
            Assert.AreEqual(UiCommandRejectReason.SkitIntentRejected, rejected.Reason);
            Assert.AreEqual("stale_revision", rejected.ToWireCode());
        }

        [Test]
        public void 会話の拒否は警告とトーストで知らせる()
        {
            var current = _store.GetCurrent();
            LogAssert.Expect(LogType.Warning, "[UiCommandResultToaster] skit.select failed: intent_not_allowed");
            Toaster.Report("skit.select", _commands.Select(current.SessionId, current.SceneRevision, "route-a"));

            Assert.IsFalse(UiCommandRejectReasonTraits.IsBenign(UiCommandRejectReason.SkitIntentRejected));
            Assert.AreEqual(1, ShownToasts.Count);
            Assert.AreEqual("The conversation did not accept that operation", ShownToasts[0].Text);
            Assert.AreEqual(GameUiToastVariant.Error, ShownToasts[0].Variant);
        }
    }
}
