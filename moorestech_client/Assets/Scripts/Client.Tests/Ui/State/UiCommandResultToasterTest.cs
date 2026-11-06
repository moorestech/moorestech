using System.Collections.Generic;
using Client.Localization;
using Client.Ui.State.Commands;
using Client.Ui.State.Toast;
using NUnit.Framework;
using UniRx;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.Ui.State
{
    /// <summary>
    /// コマンドの結果のトーストの出し方を 1 か所で決める Toaster を確かめる（L0・設計書 A-15）
    /// Verifies the toaster that decides in one place how command results become toasts (L0, design A-15)
    /// </summary>
    public class UiCommandResultToasterTest
    {
        private GameUiToastFeed _toasts;
        private UiCommandResultToaster _toaster;
        private readonly List<GameUiToast> _shown = new();

        [SetUp]
        public void SetUp()
        {
            Localize.Initialize();
            _toasts = new GameUiToastFeed();
            _toaster = new UiCommandResultToaster(_toasts);
            _shown.Clear();
            _toasts.OnToast.Subscribe(_shown.Add);
        }

        [Test]
        public void AcceptedShowsNothingAndLogsNothing()
        {
            _toaster.Report("localization.setLocale", UiCommandAccepted.Instance);
            Assert.IsEmpty(_shown);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void RejectedLogsCommandAndWireCodeThenShowsTranslatedError()
        {
            // 開発者にはコマンド名と失敗コード、利用者には理由ごとの翻訳文が届く
            // Developers get the command name and failure code; players get the per-reason translated message
            var rejected = new UiCommandRejected(UiCommandRejectReason.UnknownLanguage, "");
            LogAssert.Expect(LogType.Warning, $"[UiCommandResultToaster] localization.setLocale failed: {rejected.ToWireCode()}");
            _toaster.Report("localization.setLocale", rejected);

            Assert.AreEqual(1, _shown.Count);
            Assert.AreEqual(GameUiToastVariant.Error, _shown[0].Variant);
            Assert.AreEqual(Localize.Get(UiCommandRejectReasonTraits.ToLocalizationKey(UiCommandRejectReason.UnknownLanguage)), _shown[0].Text);
        }

        [Test]
        public void RejectedWithDetailLogsTheDetailAsWireCode()
        {
            var rejected = new UiCommandRejected(UiCommandRejectReason.BundleWriteFailed, "bundle_write_failed:disk_full");
            LogAssert.Expect(LogType.Warning, "[UiCommandResultToaster] bug_report.submit failed: bundle_write_failed:disk_full");
            _toaster.Report("bug_report.submit", rejected);
            Assert.AreEqual(1, _shown.Count);
        }
    }
}
