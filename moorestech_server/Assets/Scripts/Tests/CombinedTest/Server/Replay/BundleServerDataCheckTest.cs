using System;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Server.Boot.Replay;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.CombinedTest.Server.Replay
{
    // 記録時と違うサーバーデータを渡すと、マスタローダーが data[NN] のような読み解けない例外で落ちていた（2026-09-12 実測）
    // Passing server data other than the recording's used to die in the master loader with an unreadable exception like data[NN] (measured 2026-09-12)
    public class BundleServerDataCheckTest
    {
        private string _bundle;

        [SetUp]
        public void CreateBundle()
        {
            _bundle = Path.Combine(Path.GetTempPath(), $"moorestech-bundle-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_bundle);
        }

        [TearDown]
        public void DeleteBundle()
        {
            Directory.Delete(_bundle, true);
        }

        [Test]
        public void 記録時と同じサーバーデータなら食い違いは無い()
        {
            WriteManifest("moorestech_client/Assets/ServerData", "/report/moorestech_client/Assets/ServerData");
            var given = CreateServerData(Path.Combine("worktree", "moorestech_client", "Assets", "ServerData"));

            Assert.IsNull(BundleServerDataCheck.FindMismatch(_bundle, given));
        }

        [Test]
        public void 記録時と違うサーバーデータは期待と実際を添えて拒否される()
        {
            WriteManifest("moorestech_client/Assets/ServerData", "/report/moorestech_client/Assets/ServerData");
            var given = CreateServerData(Path.Combine("master-worktree", "server_v8"));

            var mismatch = BundleServerDataCheck.FindMismatch(_bundle, given);

            StringAssert.Contains("moorestech_client/Assets/ServerData", mismatch);
            StringAssert.Contains(given, mismatch);
        }

        [Test]
        public void サーバーデータでないディレクトリはmods不在として拒否される()
        {
            WriteManifest("server_v8", "/report/server_v8");
            var given = Path.Combine(_bundle, "not-server-data");
            Directory.CreateDirectory(given);

            StringAssert.Contains("mods/", BundleServerDataCheck.FindMismatch(_bundle, given));
        }

        [Test]
        public void 渡されなかった場合も理由付きで拒否される()
        {
            WriteManifest("server_v8", "/report/server_v8");

            StringAssert.Contains("渡されていません", BundleServerDataCheck.FindMismatch(_bundle, ""));
        }

        // serverData を持たない古い箱は突き合わせを諦めるが、黙って通さず理由を開発者へ出す
        // An older box without serverData forfeits the check, but the reason is told to the developer rather than passed in silence
        [Test]
        public void serverDataの無い箱は理由をログして続行する()
        {
            File.WriteAllText(Path.Combine(_bundle, "manifest.json"), "{\"schemaVersion\":1}");
            var given = CreateServerData("server_v8");
            LogAssert.Expect(LogType.Warning, new Regex("serverData"));

            Assert.IsNull(BundleServerDataCheck.FindMismatch(_bundle, given));
        }

        private void WriteManifest(string relativePath, string recordedPath)
        {
            var json = $"{{\"serverData\":{{\"path\":\"{recordedPath}\",\"relativeTo\":\"repository\",\"relativePath\":\"{relativePath}\"}}}}";
            File.WriteAllText(Path.Combine(_bundle, "manifest.json"), json);
        }

        private string CreateServerData(string relativePath)
        {
            var directory = Path.Combine(_bundle, relativePath);
            Directory.CreateDirectory(Path.Combine(directory, "mods"));
            return directory;
        }
    }
}
