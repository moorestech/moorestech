using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Client.Game.InGame.BugReport;
using Client.Game.InGame.BugReport.Capture;
using Client.Game.InGame.BugReport.Playtest;
using Client.RemoteExec;
using Client.RemoteExec.Access;
using Client.Tests.RemoteExec;
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

        // 箱へ印を載せる配線を守る。bug経路はcrash経路と別呼び出しなので、片方だけ配線が落ちても気づける専用ケースにする
        // Pins the wiring that attaches the mark to the box; the bug path calls it separately from the crash path, so a dropped wiring on one side alone is still caught
        [Test]
        public async Task WriteAsync経由で起動フラグ有効な遠隔実行印がmanifestへ渡る()
        {
            RemoteExecLaunchOption.ResolveFromCommandLine(new[] { RemoteExecLaunchOption.Marker });
            try
            {
                var writer = new BugReportBundleWriter(new FakeIdentity());
                var data = new BugReportCapturedData { CaptureId = 1, ReportTick = 0, Missing = new() };

                var result = await writer.WriteAsync(data, "遠隔実行印配線テスト", PlaytestReportKind.Bug);
                try
                {
                    Assert.IsTrue(result.Ready, "manifestの書き出しに失敗した");
                    var manifestPath = Path.Combine(result.BundleDirectory, BugReportBundleLayout.ManifestFileName);
                    var manifest = JObject.Parse(File.ReadAllText(manifestPath));
                    Assert.IsNotNull(manifest["remoteExec"], "起動フラグ有効なのにremoteExecがmanifestに立っていない");
                }
                finally
                {
                    Directory.Delete(result.BundleDirectory, true);
                }
            }
            finally
            {
                RemoteExecLaunchOption.ResolveFromCommandLine(new string[0]);
            }
        }

        [Test]
        public async Task WriteAsync経由で台帳の中身と相対パスがmanifestへ渡る()
        {
            // 実ユーザーデータを退避し、台帳のある有効セッションを箱の入口から通す
            // Preserve real user files while exercising an enabled session with a ledger through the writer
            var files = new RemoteExecTestFiles();
            RemoteExecLaunchOption.ResolveFromCommandLine(new[] { RemoteExecLaunchOption.Marker });
            BugReportBundleResult result = null;
            try
            {
                Directory.CreateDirectory(RemoteExecAccessFile.DirectoryPath);
                File.WriteAllText(RemoteExecLedger.CurrentPath, "current-session-ledger");
                var writer = new BugReportBundleWriter(new FakeIdentity());
                var data = new BugReportCapturedData { CaptureId = 1, ReportTick = 0, Missing = new() };
                result = await writer.WriteAsync(data, "台帳配線テスト", PlaytestReportKind.Bug);

                Assert.IsTrue(result.Ready);
                var manifest = JObject.Parse(File.ReadAllText(Path.Combine(result.BundleDirectory, BugReportBundleLayout.ManifestFileName)));
                var relativePath = $"{BugReportBundleLayout.RemoteExecDirectoryName}/{RemoteExecLedger.CurrentFileName}";
                CollectionAssert.Contains(((JArray)manifest["remoteExec"]["ledgerFiles"]).Select(x => (string)x).ToArray(), relativePath);
                Assert.AreEqual("current-session-ledger", File.ReadAllText(Path.Combine(result.BundleDirectory,
                    BugReportBundleLayout.RemoteExecDirectoryName, RemoteExecLedger.CurrentFileName)));
            }
            finally
            {
                if (result?.BundleDirectory != null && Directory.Exists(result.BundleDirectory)) Directory.Delete(result.BundleDirectory, true);
                RemoteExecLaunchOption.ResolveFromCommandLine(new string[0]);
                files.Restore();
            }
        }
    }
}
