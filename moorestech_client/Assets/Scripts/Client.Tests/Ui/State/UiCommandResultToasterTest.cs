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
            var rejected = UiCommandRejected.Of(UiCommandRejectReason.UnknownLanguage);
            LogAssert.Expect(LogType.Warning, $"[UiCommandResultToaster] localization.setLocale failed: {rejected.ToWireCode()}");
            _toaster.Report("localization.setLocale", rejected);

            Assert.AreEqual(1, _shown.Count);
            Assert.AreEqual(GameUiToastVariant.Error, _shown[0].Variant);
            Assert.AreEqual(Localize.Get(UiCommandRejectReasonTraits.ToLocalizationKey(UiCommandRejectReason.UnknownLanguage)), _shown[0].Text);
        }

        [Test]
        public void RejectedWithWireCodeOverrideLogsTheOverride()
        {
            var rejected = UiCommandRejected.OverridingWireCode(UiCommandRejectReason.BundleWriteFailed, "bundle_write_failed:disk_full");
            LogAssert.Expect(LogType.Warning, "[UiCommandResultToaster] bug_report.submit failed: bundle_write_failed:disk_full");
            _toaster.Report("bug_report.submit", rejected);
            Assert.AreEqual(1, _shown.Count);
        }

        [Test]
        public void RejectedWithDiagnosticLogsReasonCodeAndDiagnosticInOneLine()
        {
            // 診断はログだけに載り、Web へ返す失敗コードは理由の既定のまま（A-260）
            // The diagnostic rides only in the log; the wire code stays the reason's code (A-260)
            var rejected = UiCommandRejected.WithDiagnostic(UiCommandRejectReason.TrainStationNameFailed, "DuplicateName");
            Assert.AreEqual(UiCommandRejectReasonTraits.ToWireCode(UiCommandRejectReason.TrainStationNameFailed), rejected.ToWireCode());
            LogAssert.Expect(LogType.Warning, $"[UiCommandResultToaster] train_station.set_name failed: {rejected.ToWireCode()} (DuplicateName)");
            _toaster.Report("train_station.set_name", rejected);
            Assert.AreEqual(1, _shown.Count);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void SpecialBlockFailureToastShowsTheFailureCode()
        {
            // 特殊族の失敗は文言の {code} に失敗コードが入る。枠の無い文言は余った引数を使わない（A-261）
            // A special block failure fills {code} with the failure code; texts without the slot ignore the extra param (A-261)
            LogAssert.Expect(LogType.Warning, "[UiCommandResultToaster] rocket_launch_pad.command failed: rocket_command_missing_materials");
            _toaster.Report("rocket_launch_pad.command", UiCommandRejected.OverridingWireCode(UiCommandRejectReason.SpecialBlockRequestFailed, "rocket_command_missing_materials"));
            Assert.AreEqual(1, _shown.Count);
            StringAssert.Contains("rocket_command_missing_materials", _shown[0].Text);
            LogAssert.NoUnexpectedReceived();
        }
    }
}
