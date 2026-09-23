using Client.Game.InGame.BugReport.Playtest;
using Client.Localization;
using Client.PlaytestReceiver.Gate;
using Client.Starter.Playtest.TitleGates;
using Client.Tests.PlaytestReceiver;
using NUnit.Framework;

namespace Client.Tests.Playtest.TitleGates
{
    // 関所の1段目（照合）が止めた起動は、2段目のタイトルのゲートを始めず開始も通さないことを押さえる（ADR 0065）
    // Pins that a boot stopped by the checkpoint's first stage (the launch check) neither begins the title gates nor passes a start (ADR 0065)
    public class PlaytestTitleGatesBlockedVerdictTest
    {
        [SetUp]
        public void SetUp()
        {
            Localize.Initialize();
            PlaytestTitleGates.ResetOnPlayMode();
            PlaytestStartGateBypass.ResetOnPlayMode();
            PlaytestLaunchGate.SetCurrent(PlaytestGateResult.Blocked(PlaytestGateStatus.NotAllowed, "test blocked"));
        }

        [TearDown]
        public void TearDown()
        {
            PlaytestTitleGates.ResetOnPlayMode();
            PlaytestStartGateBypass.ResetOnPlayMode();
            PlaytestLaunchGate.SetCurrent(PlaytestGateResult.NotEvaluated);
        }

        // 照合の拒否は画面に確認が無い拒否なので、照合の理由をテスターへ出す。直接起動の明示通過も照合より先には効かない
        // A launch-check refusal has no confirmation on screen, so its reason goes to the tester; a direct boot's explicit pass never overrides the check
        [Test]
        public void 照合が止めた起動は開始を文言付きで断る()
        {
            Assert.AreEqual(PlaytestStartVerdict.RefusedWithNotice, PlaytestTitleGates.EvaluateStart("test", out var refusal));
            Assert.IsNotEmpty(refusal.NoticeText, "照合の拒否理由の文言が無い");

            PlaytestStartGateBypass.DeclareDirectBoot("test direct boot");
            Assert.AreEqual(PlaytestStartVerdict.RefusedWithNotice, PlaytestTitleGates.EvaluateStart("test", out _), "直接起動の明示通過が照合の拒否を素通しさせている");
        }

        // 照合を通っていない結果ではタイトルのゲートを始めない。始めると退避と確認が照合より先に走る
        // The title gates never begin on an unpassed verdict; beginning would run the salvage and confirmations ahead of the check
        [Test]
        public void 照合が止めた結果ではタイトルのゲートを始めない()
        {
            var uploads = new RecordingUploadRequester();

            Assert.IsFalse(PlaytestTitleGates.TryBegin(PlaytestLaunchGate.Current.Value, uploads, out var sequence));
            Assert.IsNull(sequence, "照合が止めたのに列が返っている");
            Assert.AreEqual(0, uploads.RequestCount);
        }
    }
}
