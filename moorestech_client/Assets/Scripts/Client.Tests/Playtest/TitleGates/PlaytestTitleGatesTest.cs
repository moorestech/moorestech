using System.IO;
using System.Threading;
using Client.Game.InGame.BugReport.Playtest;
using Client.Starter.Playtest.TitleGates;
using Client.Tests.BugReport;
using Client.Tests.PlaytestReceiver;
using Cysharp.Threading.Tasks;
using NUnit.Framework;

namespace Client.Tests.Playtest.TitleGates
{
    // 開始経路の関所と、無人・対話ごとのゲート一式の組み方を押さえる（ADR 0065）
    // Pins the start-path checkpoint and how the gate set is assembled for unattended and attended boots (ADR 0065)
    public class PlaytestTitleGatesTest
    {
        private bool _consentExisted;

        [SetUp]
        public void SetUp()
        {
            _consentExisted = PlaytestConsentFlag.IsAcknowledged();
        }

        // 段階は静的に持つので毎回未開始へ戻す。既読フラグは元の有無に戻す
        // The step is held statically, so it returns to NotStarted every time; the read flag returns to its original presence
        [TearDown]
        public void TearDown()
        {
            PlaytestTitleGates.SetStep(PlaytestTitleGateStep.NotStarted);
            var exists = File.Exists(PlaytestConsentFlag.FilePath);
            if (_consentExisted && !exists) PlaytestConsentFlag.Acknowledge();
            if (!_consentExisted && exists) File.Delete(PlaytestConsentFlag.FilePath);
        }

        [Test]
        public void 通過するまで開始を断り通過したら通す()
        {
            PlaytestTitleGates.SetStep(PlaytestTitleGateStep.NotStarted);
            Assert.IsFalse(PlaytestTitleGates.TryPassStart("test"));
            PlaytestTitleGates.SetStep(PlaytestTitleGateStep.Consent);
            Assert.IsFalse(PlaytestTitleGates.TryPassStart("test"));
            PlaytestTitleGates.SetStep(PlaytestTitleGateStep.CrashReport);
            Assert.IsFalse(PlaytestTitleGates.TryPassStart("test"));
            PlaytestTitleGates.SetStep(PlaytestTitleGateStep.Passed);
            Assert.IsTrue(PlaytestTitleGates.TryPassStart("test"));
        }

        [Test]
        public void 無人起動は異常終了があっても即通過し既読なら送る()
        {
            PlaytestConsentFlag.Acknowledge();
            var uploads = new RecordingUploadRequester();

            PlaytestTitleGates.Compose(TestPreviousSessionArtifacts.Unclean(), true, uploads, "batchMode").RunAsync(CancellationToken.None).Forget();

            Assert.AreEqual(PlaytestTitleGateStep.Passed, PlaytestTitleGates.Step.Value);
            Assert.AreEqual(1, uploads.RequestCount);
        }

        [Test]
        public void 無人起動で未読なら通過するが持ち越しを送らない()
        {
            if (File.Exists(PlaytestConsentFlag.FilePath)) File.Delete(PlaytestConsentFlag.FilePath);
            var uploads = new RecordingUploadRequester();

            PlaytestTitleGates.Compose(TestPreviousSessionArtifacts.Clean(), true, uploads, "batchMode").RunAsync(CancellationToken.None).Forget();

            Assert.AreEqual(PlaytestTitleGateStep.Passed, PlaytestTitleGates.Step.Value);
            Assert.AreEqual(0, uploads.RequestCount);
        }

        [Test]
        public void 対話起動で未読なら同意から始める()
        {
            if (File.Exists(PlaytestConsentFlag.FilePath)) File.Delete(PlaytestConsentFlag.FilePath);
            var uploads = new RecordingUploadRequester();

            PlaytestTitleGates.Compose(TestPreviousSessionArtifacts.Clean(), true, uploads, null).RunAsync(CancellationToken.None).Forget();

            Assert.AreEqual(PlaytestTitleGateStep.Consent, PlaytestTitleGates.Step.Value);
            Assert.AreEqual(0, uploads.RequestCount);
            Assert.IsFalse(PlaytestTitleGates.TryPassStart("test"));
        }

        [Test]
        public void 対話起動で既読かつ異常終了なら確認から始める()
        {
            PlaytestConsentFlag.Acknowledge();
            var uploads = new RecordingUploadRequester();

            PlaytestTitleGates.Compose(TestPreviousSessionArtifacts.Unclean(), true, uploads, null).RunAsync(CancellationToken.None).Forget();

            Assert.AreEqual(PlaytestTitleGateStep.CrashReport, PlaytestTitleGates.Step.Value);
            Assert.AreEqual(1, uploads.RequestCount);
        }
    }
}
