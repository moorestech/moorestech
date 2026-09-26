using System.IO;
using System.Threading;
using Client.Game.InGame.BugReport.Playtest;
using Client.Localization;
using Client.PlaytestReceiver.Launch;
using Client.Starter.Playtest.TitleGates;
using Client.Tests.BugReport;
using Client.Tests.PlaytestReceiver;
using Cysharp.Threading.Tasks;
using NUnit.Framework;

namespace Client.Tests.Playtest.TitleGates
{
    // 無人の開始役（smoke）がタイトルの列の始動と通過を待てることを押さえる
    // Pins that an unattended starter (the smoke run) can wait for the title sequence to start and pass
    public class PlaytestTitleGatesWaitTest
    {
        private bool _consentExisted;

        [SetUp]
        public void SetUp()
        {
            Localize.Initialize();
            PlaytestTitleGates.ResetOnPlayMode();
            PlaytestStartGateBypass.ResetOnPlayMode();
            PlaytestLaunchProfile.Apply(PlaytestLaunchKind.DeveloperMode, new EmptyPlaytestSessionIdentity(EmptyPlaytestSessionIdentity.DeveloperModeReason));
            _consentExisted = PlaytestConsentFlag.IsAcknowledged();
        }

        [TearDown]
        public void TearDown()
        {
            PlaytestTitleGates.ResetOnPlayMode();
            PlaytestStartGateBypass.ResetOnPlayMode();
            PlaytestLaunchProfile.ResetOnPlayMode();
            var exists = File.Exists(PlaytestConsentFlag.FilePath);
            if (_consentExisted && !exists) PlaytestConsentFlag.Acknowledge();
            if (!_consentExisted && exists) File.Delete(PlaytestConsentFlag.FilePath);
        }

        // 開始役はタイトルの合成ルートより先に動く。列が始まる前から待ち始め、通過した時点で解ける
        // The starter runs before the title composition root: it begins waiting before the sequence exists and is released once it passes
        [Test]
        public void 列の始動前から待ち通過で解ける()
        {
            var wait = PlaytestTitleGates.WaitUntilPassedAsync(CancellationToken.None);
            Assert.AreEqual(UniTaskStatus.Pending, wait.Status, "列が始まる前に待ちが解けている");

            // 未読の同意で止まっている間は解けない
            // It stays pending while the unread consent holds the sequence
            if (File.Exists(PlaytestConsentFlag.FilePath)) File.Delete(PlaytestConsentFlag.FilePath);
            var sequence = PlaytestTitleGates.BeginComposed(TestPreviousSessionArtifacts.Clean(), true, new RecordingUploadRequester(), null, CancellationToken.None);
            Assert.AreEqual(UniTaskStatus.Pending, wait.Status, "確認が未応答なのに待ちが解けている");

            sequence.AcknowledgeConsent();
            Assert.AreEqual(UniTaskStatus.Succeeded, wait.Status, "通過したのに待ちが解けていない");
        }

        // smokeと同じ無人起動の組み方では、始動と同時に通過して待ちが解ける
        // With the same unattended composition the smoke uses, the wait is released as soon as the sequence starts
        [Test]
        public void 無人起動の列は始動と同時に待ちを解く()
        {
            var wait = PlaytestTitleGates.WaitUntilPassedAsync(CancellationToken.None);

            PlaytestTitleGates.BeginComposed(TestPreviousSessionArtifacts.Unclean(), true, new RecordingUploadRequester(), "playtestSmoke", CancellationToken.None);

            Assert.AreEqual(UniTaskStatus.Succeeded, wait.Status);
        }
    }
}
