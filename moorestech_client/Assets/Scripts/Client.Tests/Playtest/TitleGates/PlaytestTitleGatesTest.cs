using System.IO;
using System.Threading;
using Client.Game.InGame.BugReport.Playtest;
using Client.Localization;
using Client.PlaytestReceiver.Gate;
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

        // 関所の状態は静的なので、再生し直しと同じ入口で毎回戻す。照合は開発者モードに固定して2段目だけを見る
        // The checkpoint's state is static, so it is reset every time through the same entry a replay uses; the launch check is pinned to developer mode so only the second stage is observed
        [SetUp]
        public void SetUp()
        {
            Localize.Initialize();
            PlaytestTitleGates.ResetOnPlayMode();
            PlaytestLaunchGate.SetCurrent(PlaytestGateResult.DeveloperMode);
            _consentExisted = PlaytestConsentFlag.IsAcknowledged();
        }

        [TearDown]
        public void TearDown()
        {
            PlaytestTitleGates.ResetOnPlayMode();
            PlaytestLaunchGate.SetCurrent(PlaytestGateResult.NotEvaluated);
            var exists = File.Exists(PlaytestConsentFlag.FilePath);
            if (_consentExisted && !exists) PlaytestConsentFlag.Acknowledge();
            if (!_consentExisted && exists) File.Delete(PlaytestConsentFlag.FilePath);
        }

        // 段階の正本は現行の列。列が無い＝まだ始まっていないので、テスター向けの文言を付けて断る
        // The running sequence is the step's authority; no sequence means not started yet, so the refusal carries a tester-facing text
        [Test]
        public void 通過するまで開始を断り通過したら通す()
        {
            Assert.IsFalse(PlaytestTitleGates.TryPassStart("test", out var notStartedText));
            Assert.IsNotEmpty(notStartedText, "確認が画面に出ていないのに拒否理由の文言が無い");

            var sequence = StartAttendedSequenceWithUnreadConsent();
            Assert.IsFalse(PlaytestTitleGates.TryPassStart("test", out var consentText));
            Assert.IsEmpty(consentText, "確認が画面に出ているのに別の文言を重ねている");

            sequence.AcknowledgeConsent();
            Assert.AreEqual(PlaytestTitleGateStep.Passed, sequence.Step.Value);
            Assert.IsTrue(PlaytestTitleGates.TryPassStart("test", out _));
        }

        // タイトルを通らない起動は明示的に通す。確認は出さず、未応答の印は次にタイトルを通る起動が聞き直す（D1 裁定）
        // A boot that skips the title is passed explicitly; no confirmation is shown and the next boot through the title asks again (D1 adjudication)
        [Test]
        public void タイトルを通らない起動は明示通過で開始できる()
        {
            PlaytestTitleGates.MarkPassedForDirectBoot("test direct boot");

            Assert.IsTrue(PlaytestTitleGates.TryPassStart("test", out _));

            // 直接起動の後にタイトルへ戻れば、始まった列の段階が正本になり未応答の確認で止まる（D-C1）
            // After a direct boot a return to the title makes the started sequence the authority again and the unanswered confirmation stops the start (D-C1)
            StartAttendedSequenceWithUnreadConsent();
            Assert.IsFalse(PlaytestTitleGates.TryPassStart("test", out _), "明示通過が未応答の確認を素通しさせている");
        }

        [Test]
        public void 無人起動は異常終了があっても即通過し既読なら送る()
        {
            PlaytestConsentFlag.Acknowledge();
            var uploads = new RecordingUploadRequester();

            var sequence = Start(PlaytestTitleGates.Compose(TestPreviousSessionArtifacts.Unclean(), true, uploads, "batchMode"));

            Assert.AreEqual(PlaytestTitleGateStep.Passed, sequence.Step.Value);
            Assert.AreEqual(1, uploads.RequestCount);
        }

        [Test]
        public void 無人起動で未読なら通過するが持ち越しを送らない()
        {
            if (File.Exists(PlaytestConsentFlag.FilePath)) File.Delete(PlaytestConsentFlag.FilePath);
            var uploads = new RecordingUploadRequester();

            var sequence = Start(PlaytestTitleGates.Compose(TestPreviousSessionArtifacts.Clean(), true, uploads, "batchMode"));

            Assert.AreEqual(PlaytestTitleGateStep.Passed, sequence.Step.Value);
            Assert.AreEqual(0, uploads.RequestCount);
        }

        [Test]
        public void 対話起動で未読なら同意から始める()
        {
            var uploads = new RecordingUploadRequester();
            var sequence = StartAttendedSequenceWithUnreadConsent(uploads);

            Assert.AreEqual(PlaytestTitleGateStep.Consent, sequence.Step.Value);
            Assert.AreEqual(0, uploads.RequestCount);
            Assert.IsFalse(PlaytestTitleGates.TryPassStart("test", out _));
        }

        [Test]
        public void 対話起動で既読かつ異常終了なら確認から始める()
        {
            PlaytestConsentFlag.Acknowledge();
            var uploads = new RecordingUploadRequester();

            var sequence = Start(PlaytestTitleGates.Compose(TestPreviousSessionArtifacts.Unclean(), true, uploads, null));

            Assert.AreEqual(PlaytestTitleGateStep.CrashReport, sequence.Step.Value);
            Assert.AreEqual(1, uploads.RequestCount);
        }

        [Test]
        public void 開発者モードは既読でも送信要求を出さない()
        {
            // receiverSessionAllowedがfalse（開発者モード）なら、了解済みでも送信は要求されない
            // With receiverSessionAllowed false (developer mode), no upload is requested even when consent is already acknowledged
            PlaytestConsentFlag.Acknowledge();
            var uploads = new RecordingUploadRequester();

            var sequence = Start(PlaytestTitleGates.Compose(TestPreviousSessionArtifacts.Clean(), false, uploads, null));

            Assert.AreEqual(PlaytestTitleGateStep.Passed, sequence.Step.Value);
            Assert.AreEqual(0, uploads.RequestCount);
        }

        private static PlaytestTitleGateSequence StartAttendedSequenceWithUnreadConsent()
        {
            return StartAttendedSequenceWithUnreadConsent(new RecordingUploadRequester());
        }

        private static PlaytestTitleGateSequence StartAttendedSequenceWithUnreadConsent(RecordingUploadRequester uploads)
        {
            if (File.Exists(PlaytestConsentFlag.FilePath)) File.Delete(PlaytestConsentFlag.FilePath);
            return Start(PlaytestTitleGates.Compose(TestPreviousSessionArtifacts.Clean(), true, uploads, null));
        }

        // 本番と同じく、組んだ列を現行として据えてから進める。開始経路はこの列の段階を読む
        // Just as in production, the composed sequence is installed as the running one before it advances, and the start paths read its step
        private static PlaytestTitleGateSequence Start(PlaytestTitleGateSequence sequence)
        {
            PlaytestTitleGates.SetCurrentSequence(sequence);
            sequence.RunAsync(CancellationToken.None).Forget();
            return sequence;
        }
    }
}
