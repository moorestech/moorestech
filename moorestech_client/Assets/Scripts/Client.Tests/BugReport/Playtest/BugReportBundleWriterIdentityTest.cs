using System.IO;
using System.Threading.Tasks;
using Client.Game.InGame.BugReport;
using Client.Game.InGame.BugReport.Capture;
using Client.Game.InGame.BugReport.Playtest;
using Game.Paths;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Client.Tests.BugReport
{
    // WriteAsync経由でidentityのSteamIdが実際にmanifestへ配線されているかを検証する（task-4レビュー Minor 2）
    // Verifies that identity's SteamId is actually wired into the manifest via WriteAsync (task-4 review Minor 2)
    public class BugReportBundleWriterIdentityTest
    {
        private sealed class FakeIdentity : IPlaytestSessionIdentity
        {
            public string SteamId => "steam-76500000000000001";
            public string SteamIdAbsenceReason => null;
        }

        [Test]
        public async Task WriteAsync経由でsteamIdがmanifestへ渡りbuildInfoがnullでも壊れない()
        {
            var writer = new BugReportBundleWriter(new FakeIdentity());
            var data = new BugReportCapturedData { CaptureId = 1, ReportTick = 0, Missing = new() };

            var result = await writer.WriteAsync(data, "identity配線テスト", PlaytestReportKind.Bug);
            try
            {
                Assert.IsTrue(result.Ready, "manifestの書き出しに失敗した");
                var manifestPath = Path.Combine(result.BundleDirectory, BugReportBundleLayout.ManifestFileName);
                var manifest = JObject.Parse(File.ReadAllText(manifestPath));

                Assert.AreEqual("steam-76500000000000001", (string)manifest["steamId"], "identityのSteamIdがmanifestへ渡っていない");

                // Editorでは buildInfo が常にnullなので、build-info.json不在時の縮退がそのまま検証できる
                // In the Editor buildInfo is always null, so the "build-info.json absent" degradation is exercised as-is
                Assert.IsTrue(manifest.ContainsKey("buildInfo"), "buildInfoキー自体が無い");
                Assert.AreEqual(JTokenType.Null, manifest["buildInfo"].Type, "buildInfoがnullでない");
            }
            finally
            {
                Directory.Delete(result.BundleDirectory, true);
            }
        }
    }
}
