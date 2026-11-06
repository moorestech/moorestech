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
    /// Toasterの振り分けを確かめる（L0）
    /// Verifies the toaster routing (L0)
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
            // 開発者向けログと利用者向け翻訳文
            // Developer log and player translation
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
