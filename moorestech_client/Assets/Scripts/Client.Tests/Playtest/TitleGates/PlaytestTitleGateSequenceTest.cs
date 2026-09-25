using System.IO;
using System.Threading;
using Client.Game.InGame.BugReport.Playtest;
using Client.Starter.Playtest.TitleGates;
using Client.Tests.BugReport;
using Client.Tests.PlaytestReceiver;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.Playtest.TitleGates
{
    // 「同意 → 前回異常終了の確認 → 開始を受け付ける」の順序と、持ち越しの送信要求を置く位置を押さえる
    // Pins consent → previous-crash confirmation → accepting the start, and the carried-over upload point
    public class PlaytestTitleGateSequenceTest
    {
        private const string WrittenDirectory = "/tmp/crash-bundle-double";

        private bool _consentExisted;

        // 了解は本番と同じ場所へ既読フラグを書く。自分が作った分だけ後始末する
        // Acknowledging writes the read flag to the production location, so only what this test creates is cleaned up
        [SetUp]
        public void SetUp()
        {
            _consentExisted = PlaytestConsentFlag.IsAcknowledged();
        }

        [TearDown]
        public void TearDown()
        {
            if (!_consentExisted && File.Exists(PlaytestConsentFlag.FilePath)) File.Delete(PlaytestConsentFlag.FilePath);
        }

        [Test]
        public void 既読かつ正常終了なら始めた時点で通過し送信を1回要求する()
        {
            var uploads = new RecordingUploadRequester();
            var sequence = Sequence(false, new CrashReportGate(new RecordingCrashBundleWriter(WrittenDirectory), TestPreviousSessionArtifacts.Clean()), uploads, true);

            sequence.RunAsync(CancellationToken.None).Forget();

            Assert.AreEqual(PlaytestTitleGateStep.Passed, sequence.Step.Value);
            Assert.AreEqual(1, uploads.RequestCount);
        }

        [Test]
        public void 未読なら同意で止まり了解するまで送信を要求しない()
        {
            var uploads = new RecordingUploadRequester();
            var sequence = Sequence(true, new CrashReportGate(new RecordingCrashBundleWriter(WrittenDirectory), TestPreviousSessionArtifacts.Clean()), uploads, true);

            sequence.RunAsync(CancellationToken.None).Forget();
            Assert.AreEqual(PlaytestTitleGateStep.Consent, sequence.Step.Value);
            Assert.AreEqual(0, uploads.RequestCount, "了解の前に持ち越しを送っている");

            Assert.AreEqual(PlaytestConsentResult.Acknowledged, sequence.AcknowledgeConsent());
            Assert.AreEqual(PlaytestTitleGateStep.Passed, sequence.Step.Value);
            Assert.AreEqual(1, uploads.RequestCount);
        }

        [Test]
        public void 未読かつ異常終了なら同意の次に確認を出し箱を書いたら送信を再要求する()
        {
            var uploads = new RecordingUploadRequester();
            var writer = new RecordingCrashBundleWriter(WrittenDirectory);
            var sequence = Sequence(true, new CrashReportGate(writer, TestPreviousSessionArtifacts.Unclean()), uploads, true);

            sequence.RunAsync(CancellationToken.None).Forget();
            Assert.AreEqual(PlaytestTitleGateStep.Consent, sequence.Step.Value, "何が送られるかを見せる前に送信可否を聞いている");

            sequence.AcknowledgeConsent();
            Assert.AreEqual(PlaytestTitleGateStep.CrashReport, sequence.Step.Value);
            Assert.AreEqual(1, uploads.RequestCount);

            Assert.AreEqual(CrashReportResponseResult.Sent, sequence.RespondCrashReportAsync(true, "落ちた").GetAwaiter().GetResult());
            Assert.AreEqual(PlaytestTitleGateStep.Passed, sequence.Step.Value);
            Assert.AreEqual(2, uploads.RequestCount, "異常終了の箱を書いた後に送信を再要求していない");
            CollectionAssert.AreEqual(new[] { "落ちた" }, writer.Descriptions);
        }

        // 同意待ち中に届いた異常終了応答は段階違いとして拒否する。通してしまうと了解前の送信要求と段階の飛び越しが起きる
        // An unclean-exit answer arriving while consent is still pending is refused as the wrong step; letting it through would request an upload before consent and skip a step
        [Test]
        public void 同意段階中の異常終了応答は拒否され了解後にCrashReport段階へ進む()
        {
            var uploads = new RecordingUploadRequester();
            var writer = new RecordingCrashBundleWriter(WrittenDirectory);
            var sequence = Sequence(true, new CrashReportGate(writer, TestPreviousSessionArtifacts.Unclean()), uploads, true);

            sequence.RunAsync(CancellationToken.None).Forget();
            Assert.AreEqual(PlaytestTitleGateStep.Consent, sequence.Step.Value);

            Assert.AreEqual(CrashReportResponseResult.NotAsked, sequence.RespondCrashReportAsync(true, "早すぎる").GetAwaiter().GetResult());
            Assert.AreEqual(PlaytestTitleGateStep.Consent, sequence.Step.Value, "拒否されたのに段階が進んでいる");
            Assert.AreEqual(0, uploads.RequestCount, "同意前に送信を要求している");
            CollectionAssert.IsEmpty(writer.Descriptions, "同意前に箱を書いている");

            sequence.AcknowledgeConsent();
            Assert.AreEqual(PlaytestTitleGateStep.CrashReport, sequence.Step.Value);
        }

        [Test]
        public void 既読かつ異常終了なら確認だけを出し送らないでも通過して再要求しない()
        {
            var uploads = new RecordingUploadRequester();
            var sequence = Sequence(false, new CrashReportGate(new RecordingCrashBundleWriter(WrittenDirectory), TestPreviousSessionArtifacts.Unclean()), uploads, true);

            sequence.RunAsync(CancellationToken.None).Forget();
            Assert.AreEqual(PlaytestTitleGateStep.CrashReport, sequence.Step.Value);
            Assert.AreEqual(1, uploads.RequestCount);

            Assert.AreEqual(CrashReportResponseResult.Skipped, sequence.RespondCrashReportAsync(false, "").GetAwaiter().GetResult());
            Assert.AreEqual(PlaytestTitleGateStep.Passed, sequence.Step.Value);
            Assert.AreEqual(1, uploads.RequestCount);
        }

        // 書けなかった応答は答えたことにならない。確認に留まり、送らないを選べば先へ進める
        // A failed write is not an answer: the confirmation stays, and choosing not to send moves on
        [Test]
        public void 箱を書けなかったら確認に留まり送信を再要求しない()
        {
            var uploads = new RecordingUploadRequester();
            var sequence = Sequence(false, new CrashReportGate(new RecordingCrashBundleWriter(null), TestPreviousSessionArtifacts.Unclean()), uploads, true);
            sequence.RunAsync(CancellationToken.None).Forget();
            LogAssert.Expect(LogType.Error, "前回異常終了の箱を書けなかったため確認を閉じません（送り直すか、送らないを選べます）");

            Assert.AreEqual(CrashReportResponseResult.WriteFailed, sequence.RespondCrashReportAsync(true, "書けない").GetAwaiter().GetResult());
            Assert.AreEqual(PlaytestTitleGateStep.CrashReport, sequence.Step.Value);
            Assert.AreEqual(1, uploads.RequestCount);

            Assert.AreEqual(CrashReportResponseResult.Skipped, sequence.RespondCrashReportAsync(false, "").GetAwaiter().GetResult());
            Assert.AreEqual(PlaytestTitleGateStep.Passed, sequence.Step.Value);
        }

        // Consent段階を過ぎた後に届いた了解は、閉じたゲートの「もう答えた」に畳まず、段階違いとしてNotAskedを返す
        // An acknowledgement arriving after the Consent step has passed returns NotAsked for the wrong-step reason, not folded into the closed gate's "already acknowledged"
        [Test]
        public void Consent段階を過ぎた後の了解はNotAskedを返す()
        {
            var uploads = new RecordingUploadRequester();
            var sequence = Sequence(true, new CrashReportGate(new RecordingCrashBundleWriter(WrittenDirectory), TestPreviousSessionArtifacts.Unclean()), uploads, true);

            sequence.RunAsync(CancellationToken.None).Forget();
            Assert.AreEqual(PlaytestConsentResult.Acknowledged, sequence.AcknowledgeConsent());
            Assert.AreEqual(PlaytestTitleGateStep.CrashReport, sequence.Step.Value);

            Assert.AreEqual(PlaytestConsentResult.NotAsked, sequence.AcknowledgeConsent());
            Assert.AreEqual(PlaytestTitleGateStep.CrashReport, sequence.Step.Value, "段階違いの了解で段階が動いている");
        }

        [Test]
        public void 開発者モードでは箱を書いても送信を要求しない()
        {
            var uploads = new RecordingUploadRequester();
            var sequence = Sequence(true, new CrashReportGate(new RecordingCrashBundleWriter(WrittenDirectory), TestPreviousSessionArtifacts.Unclean()), uploads, false);

            sequence.RunAsync(CancellationToken.None).Forget();
            sequence.AcknowledgeConsent();
            sequence.RespondCrashReportAsync(true, "開発者").GetAwaiter().GetResult();

            Assert.AreEqual(PlaytestTitleGateStep.Passed, sequence.Step.Value);
            Assert.AreEqual(0, uploads.RequestCount);
        }

        // 打ち切りはプロセス終了のときだけ起きる（本番は Application.exitCancellationToken）。打ち切られた後の了解では通過しない
        // Cancellation happens only at process exit in production (Application.exitCancellationToken); an acknowledgement arriving after it does not pass the gates
        [Test]
        public void 打ち切られた後の了解では通過しない()
        {
            var uploads = new RecordingUploadRequester();
            var sequence = Sequence(true, new CrashReportGate(new RecordingCrashBundleWriter(WrittenDirectory), TestPreviousSessionArtifacts.Clean()), uploads, true);
            var cancellation = new CancellationTokenSource();

            sequence.RunAsync(cancellation.Token).Forget();
            cancellation.Cancel();
            sequence.AcknowledgeConsent();

            Assert.AreEqual(PlaytestTitleGateStep.Consent, sequence.Step.Value);
            Assert.AreEqual(0, uploads.RequestCount);
        }

        private static PlaytestTitleGateSequence Sequence(bool consentWaiting, CrashReportGate crashReport, RecordingUploadRequester uploads, bool uploadsEnabled)
        {
            return new PlaytestTitleGateSequence(new PlaytestConsentGate(consentWaiting), crashReport, uploads, uploadsEnabled);
        }
    }
}
