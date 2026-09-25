using System.IO;
using Client.Game.InGame.BugReport.Playtest;
using Client.PlaytestReceiver.Launch;
using Client.Starter.PlaytestSmoke;
using NUnit.Framework;

namespace Client.Tests.PlaytestSmoke
{
    public class StandalonePlaytestSmokeSettingsTest
    {
        // 既読でも開発者モードは拒否し、配布版のphase1だけを開始可能にする
        // Even with acknowledged consent reject developer mode and allow distribution phase one
        [TestCase(PlaytestLaunchKind.DeveloperMode, true)]
        [TestCase(PlaytestLaunchKind.Distribution, false)]
        public void 前提検査は配布版だけを受理する(PlaytestLaunchKind kind, bool expectedFailure)
        {
            var args = new[] { "--playtestSmoke", "--smokePhase", "phase1", "--smokeResultDirectory", "C:/smoke" };
            Assert.IsTrue(StandalonePlaytestSmokeSettings.TryParse(args, out var settings, out var error), error);
            var consentExisted = PlaytestConsentFlag.IsAcknowledged();
            try
            {
                if (!consentExisted) PlaytestConsentFlag.Acknowledge();
                Assert.AreEqual(expectedFailure, StandalonePlaytestSmokePreconditions.TryFindFailure(settings, kind, out var failureReason));
                if (expectedFailure) StringAssert.Contains("developer mode", failureReason);
                else Assert.IsEmpty(failureReason);
            }
            finally
            {
                // 元からある同意ファイルを上書きせず、作成分だけを片付ける
                // Preserve any existing consent file and clean up only the one created here
                if (!consentExisted && File.Exists(PlaytestConsentFlag.FilePath)) File.Delete(PlaytestConsentFlag.FilePath);
            }
        }

        [Test]
        public void 完全な引数を受理する()
        {
            var args = new[] { "moorestech.exe", "--playtestSmoke", "--smokePhase", "phase1", "--smokeResultDirectory", "C:/smoke" };
            Assert.IsTrue(StandalonePlaytestSmokeSettings.TryParse(args, out var settings, out var error), error);
            Assert.AreEqual(StandalonePlaytestSmokePhase.PhaseOne, settings.Phase);
            Assert.AreEqual("C:/smoke", settings.ResultDirectory);
        }

        [Test]
        public void phase2を受理し結果の段階名は引数と同じ語に戻る()
        {
            var args = new[] { "--playtestSmoke", "--smokePhase", "phase2", "--smokeResultDirectory", "C:/smoke" };
            Assert.IsTrue(StandalonePlaytestSmokeSettings.TryParse(args, out var settings, out var error), error);
            Assert.AreEqual(StandalonePlaytestSmokePhase.PhaseTwo, settings.Phase);
            Assert.AreEqual("phase2", StandalonePlaytestSmokeSettings.ToArgument(settings.Phase));
            Assert.AreEqual("phase1", StandalonePlaytestSmokeSettings.ToArgument(StandalonePlaytestSmokePhase.PhaseOne));
        }

        [Test]
        public void マーカーが無ければ起動しない()
        {
            Assert.IsFalse(StandalonePlaytestSmokeSettings.HasMarker(new[] { "moorestech.exe" }));
        }

        [Test]
        public void 未知のphaseは理由付きで拒否する()
        {
            var args = new[] { "--playtestSmoke", "--smokePhase", "phase9", "--smokeResultDirectory", "C:/smoke" };
            Assert.IsFalse(StandalonePlaytestSmokeSettings.TryParse(args, out _, out var error));
            StringAssert.Contains("phase9", error);
        }

        [Test]
        public void 欠落と空値と重複を拒否する()
        {
            Assert.IsFalse(StandalonePlaytestSmokeSettings.TryParse(
                new[] { "--playtestSmoke", "--smokeResultDirectory", "C:/smoke" }, out _, out var missing));
            StringAssert.Contains("--smokePhase", missing);

            Assert.IsFalse(StandalonePlaytestSmokeSettings.TryParse(
                new[] { "--playtestSmoke", "--smokePhase", "--smokeResultDirectory", "C:/smoke" }, out _, out var empty));
            StringAssert.Contains("--smokePhase", empty);

            Assert.IsFalse(StandalonePlaytestSmokeSettings.TryParse(
                new[] { "--playtestSmoke", "--smokePhase", "phase1", "--smokePhase", "phase2", "--smokeResultDirectory", "C:/smoke" },
                out _, out var duplicated));
            StringAssert.Contains("exactly once", duplicated);
        }
    }
}
