using System;
using System.IO;
using System.Text.RegularExpressions;
using Client.Game.InGame.Playtest.Progress;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

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

        // ヘッダ書き込み前に落ちた残骸。回収されないと次のセッションの記録へ黙って混ざる
        // A leftover from a crash before the header was written; if not recovered it silently mixes into the next session's record
        [Test]
        public void ヘッダ無しでイベントだけの残骸も回収されcurrentが空になる()
        {
            ProgressRecordFiles.AppendEvent(ProgressEventEntry.Create(DateTime.UtcNow.AddMinutes(-3), 1, ProgressEventType.CraftExecuted, new JObject { ["recipeGuid"] = "old" }));

            LogAssert.Expect(LogType.Warning, new Regex("headerMissing"));
            var bundle = ProgressSessionRecovery.RecoverLeftoverSession(false);

            var record = JObject.Parse(File.ReadAllText(Path.Combine(bundle, ProgressRecordPaths.RecordFileName)));
            Assert.IsTrue((bool)record["headerMissing"]);
            Assert.AreEqual(1, ((JArray)record["events"]).Count);
            Assert.AreEqual("crash-recovered", (string)record["endReason"]);
            Assert.IsFalse(ProgressRecordFiles.HasCurrentSession());
            Assert.IsFalse(File.Exists(ProgressRecordPaths.CurrentEventsPath));
            Directory.Delete(bundle, true);
        }

        [Test]
        public void ヘッダ無しの残骸のイベントは次のセッションの記録に混ざらない()
        {
            ProgressRecordFiles.AppendEvent(ProgressEventEntry.Create(DateTime.UtcNow.AddMinutes(-3), 1, ProgressEventType.CraftExecuted, new JObject { ["recipeGuid"] = "old" }));
            LogAssert.Expect(LogType.Warning, new Regex("headerMissing"));
            var recovered = ProgressSessionRecovery.RecoverLeftoverSession(false);

            // 回収→current/ を空にする→新セッション開始、の順序でしか新しいヘッダは書かれない
            // A new header is written only in this order: recover, empty current/, then start the new session
            var writer = new ProgressSessionWriter();
            writer.WriteHeader(new ProgressRecordHeader { SessionStart = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'") });
            writer.Append(ProgressEventEntry.Create(DateTime.UtcNow, 2, ProgressEventType.BlockPlaced, new JObject()));
            var bundle = writer.Close(ProgressSessionRecovery.QuitEndReason, DateTime.UtcNow);

            var record = JObject.Parse(File.ReadAllText(Path.Combine(bundle, ProgressRecordPaths.RecordFileName)));
            var events = (JArray)record["events"];
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(ProgressEventType.BlockPlaced, (string)events[0]["type"]);
            Assert.AreEqual(0, (int)record["craftCount"]);
            Assert.IsNull(record["headerMissing"]);
            Directory.Delete(recovered, true);
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
