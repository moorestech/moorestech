using System.IO;
using System.Linq;
using Client.Game.InGame.BugReport;
using Client.Game.InGame.BugReport.LastSession;
using Game.Paths;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Client.Tests.BugReport.Bundle
{
    // 落ちたセッションのCEF・Webホストの値は残っていないので、未試行と偽らず unknown と欠損理由を載せる
    // The crashed session's CEF and web host values are gone, so the box reads unknown with a missing reason instead of claiming notAttempted
    public class CrashBundleWebUiHealthTest
    {
        // WriteAsyncはCIのバッチモードEditModeでPlayerLoopが回らず戻れないので、同期の本体Writeを呼ぶ（前例: CrashBundleWriterTest）
        // WriteAsync never returns in CI's batch-mode EditMode without a PlayerLoop, so the synchronous Write core is called (precedent: CrashBundleWriterTest)
        [Test]
        public void 異常終了の箱はWebホストとCEFを不明と書く()
        {
            var bundle = CrashBundleWriter.Write(TestPreviousSessionArtifacts.Unclean(), "不明", RepositoryStateProbe.RepositoryRoot, RepositoryStateProbe.MasterDataRoot);
            try
            {
                var manifest = JObject.Parse(File.ReadAllText(Path.Combine(bundle, BugReportBundleLayout.ManifestFileName)));
                var health = manifest["webUiHealth"];
                Assert.AreEqual(6, (int)manifest["schemaVersion"]);
                Assert.AreEqual("unknown", (string)health["hostStartup"]);
                Assert.AreEqual("unknown", (string)health["cefProcess"]);
                Assert.AreEqual(JTokenType.Null, health["secondsSinceLastWebMessage"].Type);
                Assert.IsTrue(manifest["missing"].Any(item => (string)item["item"] == "webUiHealth"), "不明の理由が欠損列に無い");
            }
            finally
            {
                Directory.Delete(bundle, true);
            }
        }
    }
}
