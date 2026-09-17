using Client.Starter.PlaytestSmoke;
using NUnit.Framework;

namespace Client.Tests.PlaytestSmoke
{
    public class StandalonePlaytestSmokeSettingsTest
    {
        [Test]
        public void 完全な引数を受理する()
        {
            var args = new[] { "moorestech.exe", "--playtestSmoke", "--smokePhase", "phase1", "--smokeResultDirectory", "C:/smoke" };
            Assert.IsTrue(StandalonePlaytestSmokeSettings.TryParse(args, out var settings, out var error), error);
            Assert.AreEqual(StandalonePlaytestSmokeSettings.PhaseOne, settings.Phase);
            Assert.AreEqual("C:/smoke", settings.ResultDirectory);
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
