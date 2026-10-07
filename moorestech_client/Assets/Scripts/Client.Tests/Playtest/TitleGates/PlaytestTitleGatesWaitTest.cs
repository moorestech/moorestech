using System;
using System.Collections;
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
using UnityEngine.TestTools;

namespace Client.Tests.Playtest.TitleGates
{
    // 無人の開始役（smoke・出展モード）がタイトルの列の始動と通過を待て、列が始まらなければ期限で諦めることを押さえる
    // Pins that an unattended starter (smoke, exhibition mode) can wait for the title sequence to pass, and gives up at the deadline when it never starts
    public class PlaytestTitleGatesWaitTest
    {
        // 期限切れの判定に使う短い期限秒
        // Short deadline in seconds for the expiry check
        private const float ShortDeadlineSeconds = 0.2f;

        // 期限より十分長い観測上限秒。これを超えたら期限が効いていない
        // Observation limit well beyond the deadline; exceeding it means the deadline is not honored
        private const float ObservationLimitSeconds = 5f;

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

        // 列が始まらない（タイトル合成ルートが動かない配線不良）と、渡した期限で偽を返す。出展モードはこれを見てプロセスを終了する
        // When the sequence never starts (a wiring fault where the title composition root does not run), it returns false at the given deadline; exhibition mode quits the process on it
        [UnityTest]
        public IEnumerator 列が始まらなければ渡した期限で偽を返す() => UniTask.ToCoroutine(async () =>
        {
            var wait = PlaytestTitleGates.WaitUntilPassedWithinDeadlineAsync(ShortDeadlineSeconds, CancellationToken.None);
            var observationLimit = UniTask.Delay(TimeSpan.FromSeconds(ObservationLimitSeconds), DelayType.Realtime);

            var (waitFinishedFirst, passed) = await UniTask.WhenAny(wait, observationLimit);
            Assert.IsTrue(waitFinishedFirst, $"{ShortDeadlineSeconds}秒の期限を渡したのに{ObservationLimitSeconds}秒以内に待ちが解けなかった");
            Assert.IsFalse(passed, "列が始まっていないのに通過扱いになった");
        });
    }
}
