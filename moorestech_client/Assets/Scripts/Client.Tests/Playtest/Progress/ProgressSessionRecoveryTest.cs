using System;
using System.IO;
using Client.Game.InGame.Playtest.Progress;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Client.Tests.Playtest
{
    public class ProgressSessionRecoveryTest
    {
        [SetUp]
        [TearDown]
        public void ClearCurrent()
        {
            ProgressRecordFiles.ClearCurrent();
        }

        private static void WriteLeftoverSession()
        {
            ProgressRecordFiles.WriteHeader(new ProgressRecordHeader
            {
                SessionStart = DateTime.UtcNow.AddMinutes(-5).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"),
                WorldCreatedAt = "2026-09-10T09:00:00Z",
            });
            ProgressRecordFiles.AppendEvent(ProgressEventEntry.Create(DateTime.UtcNow.AddMinutes(-4), 1, ProgressEventType.BlockPlaced, new JObject()));
        }

        [Test]
        public void 残骸が無ければ何もしない()
        {
            Assert.IsNull(ProgressSessionRecovery.RecoverLeftoverSession(true));
            Assert.IsNull(ProgressSessionRecovery.RecoverLeftoverSession(false));
        }

        [Test]
        public void 異常終了の残骸はcrash_recoveredで送る()
        {
            WriteLeftoverSession();
            var bundle = ProgressSessionRecovery.RecoverLeftoverSession(false);
            var record = JObject.Parse(File.ReadAllText(Path.Combine(bundle, ProgressRecordPaths.RecordFileName)));
            Assert.AreEqual("crash-recovered", (string)record["endReason"]);
            Assert.IsFalse(ProgressRecordFiles.HasCurrentSession());
            Directory.Delete(bundle, true);
        }

        [Test]
        public void 正常終了なのに残った残骸はquitで送る()
        {
            // マーカーは書けたが record を書き切る前に落ちた場合。恒久的に残さず必ず回収する
            // The marker was written but the record was not; this leftover is always recovered, never left behind
            WriteLeftoverSession();
            var bundle = ProgressSessionRecovery.RecoverLeftoverSession(true);
            var record = JObject.Parse(File.ReadAllText(Path.Combine(bundle, ProgressRecordPaths.RecordFileName)));
            Assert.AreEqual("quit", (string)record["endReason"]);
            Assert.IsFalse(ProgressRecordFiles.HasCurrentSession());
            Directory.Delete(bundle, true);
        }

        [Test]
        public void イベントが0件の残骸も送れる()
        {
            ProgressRecordFiles.WriteHeader(new ProgressRecordHeader { SessionStart = DateTime.UtcNow.AddMinutes(-1).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'") });
            var bundle = ProgressSessionRecovery.RecoverLeftoverSession(false);
            var record = JObject.Parse(File.ReadAllText(Path.Combine(bundle, ProgressRecordPaths.RecordFileName)));
            Assert.AreEqual(0, ((JArray)record["events"]).Count);
            Directory.Delete(bundle, true);
        }
    }
}
