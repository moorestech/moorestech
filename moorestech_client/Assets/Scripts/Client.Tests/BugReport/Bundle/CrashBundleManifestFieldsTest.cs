using System.IO;
using System.Linq;
using Client.Game.InGame.BugReport;
using Client.Game.InGame.BugReport.LastSession;
using Game.Paths;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Client.Tests.BugReport.Bundle
{
    // 異常終了の箱の manifest の欄: webUiHealth は欄にも欠損列にも出ず、gameUiExceptions は不明と欠損の組で残る（ADR 0057・A-244）
    // Crash-bundle manifest fields: webUiHealth appears neither as a field nor in the missing list, while gameUiExceptions stays as unknown plus a gap entry (ADR 0057, A-244)
    public class CrashBundleManifestFieldsTest
    {
        // WriteAsyncはCIのバッチモードEditModeでPlayerLoopが回らず戻れないので、同期の本体Writeを呼ぶ（前例: CrashBundleWriterTest）
        // WriteAsync never returns in CI's batch-mode EditMode without a PlayerLoop, so the synchronous Write core is called (precedent: CrashBundleWriterTest)
        [Test]
        public void 異常終了の箱はwebUiHealthを持たずgameUiExceptionsは不明と欠損の組で持つ()
        {
            var bundle = CrashBundleWriter.Write(TestPreviousSessionArtifacts.Unclean(), "不明", RepositoryStateProbe.RepositoryRoot, RepositoryStateProbe.MasterDataRoot, TestBugReportOutboxRoot.Directory);
            try
            {
                var manifest = JObject.Parse(File.ReadAllText(Path.Combine(bundle, BugReportBundleLayout.ManifestFileName)));
                Assert.AreEqual(8, (int)manifest["schemaVersion"]);
                Assert.IsFalse(manifest.ContainsKey("webUiHealth"));
                Assert.IsFalse(manifest["missing"].Any(item => (string)item["item"] == "webUiHealth"), "欠損列に webUiHealth が残っている");

                // 落ちたセッションの UI の例外は数えないので、0 件と読ませず不明と欠損の組で残る
                // The crashed session's UI exceptions are not counted, so they stay as unknown plus a gap entry rather than reading as zero
                Assert.AreEqual("unknown", (string)manifest["gameUiExceptions"]["status"]);
                Assert.IsNull(manifest["gameUiExceptions"]["count"], "unknown は件数の欄を持たない");
                Assert.IsTrue(manifest["missing"].Any(item => (string)item["item"] == "gameUiExceptions"), "欠損列に gameUiExceptions が無い");
            }
            finally
            {
                Directory.Delete(bundle, true);
            }
        }
    }
}
