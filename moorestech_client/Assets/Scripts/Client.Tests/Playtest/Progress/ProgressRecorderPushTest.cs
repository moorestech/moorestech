using System;
using System.Collections.Generic;
using System.IO;
using Client.Game.Common;
using Client.Game.InGame.BugReport.Playtest;
using Client.Game.InGame.Playtest.Progress;
using Game.Paths;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Client.Tests.Playtest
{
    // 購読では観測できない操作のプッシュが、1回につき events.jsonl の1行になることを固定する
    // Fixes that each push of an operation no subscription observes becomes exactly one line in events.jsonl
    public class ProgressRecorderPushTest
    {
        [SetUp]
        [TearDown]
        public void ClearCurrent()
        {
            ProgressRecordFiles.ClearCurrent();
        }

        // プッシュ経路は ctor で受けた依存を一切使わない。Initialize を呼ばない限り購読も張られない
        // The push path touches none of the ctor dependencies, and no subscription is made unless Initialize runs
        private static ProgressRecorder CreateRecorderForPushOnly()
        {
            return new ProgressRecorder(null, null, new EmptyPlaytestSessionIdentity());
        }

        [Test]
        public void クラフトのプッシュが1行のcraftExecutedになる()
        {
            var recorder = CreateRecorderForPushOnly();
            var recipeGuid = Guid.NewGuid();

            recorder.RecordCraftExecuted(recipeGuid);

            var events = ProgressRecordFiles.ReadEvents();
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(ProgressEventType.CraftExecuted, events[0].Type);
            Assert.AreEqual(recipeGuid.ToString(), (string)events[0].Data["recipeGuid"]);
        }

        [Test]
        public void 報告送信のプッシュが1行のreportSentになる()
        {
            var recorder = CreateRecorderForPushOnly();

            recorder.RecordReportSent(PlaytestReportKind.Feedback);

            var events = ProgressRecordFiles.ReadEvents();
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(ProgressEventType.ReportSent, events[0].Type);
            Assert.AreEqual(PlaytestReportKind.Feedback, (string)events[0].Data["kind"]);
        }

        [Test]
        public void プッシュ回数と行数が一致する()
        {
            var recorder = CreateRecorderForPushOnly();

            recorder.RecordCraftExecuted(Guid.NewGuid());
            recorder.RecordReportSent(PlaytestReportKind.Bug);
            recorder.RecordCraftExecuted(Guid.NewGuid());

            Assert.AreEqual(3, File.ReadAllLines(ProgressRecordPaths.CurrentEventsPath).Length);
        }

        [Test]
        public void 書き出し後のプッシュは記録されない()
        {
            var recorder = CreateRecorderForPushOnly();
            ProgressRecordFiles.WriteHeader(new ProgressRecordHeader { SessionStart = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'") });
            recorder.RecordCraftExecuted(Guid.NewGuid());
            var before = Directory.Exists(GameSystemPaths.ProgressRecordOutboxDirectory) ? Directory.GetDirectories(GameSystemPaths.ProgressRecordOutboxDirectory) : Array.Empty<string>();

            Assert.AreEqual(ShutdownFlushResult.Flushed, recorder.FlushOnShutdownAsync().GetAwaiter().GetResult());
            recorder.RecordReportSent(PlaytestReportKind.Bug);

            // 閉じた後の追記は current/ を作り直してしまうため、ファイルが復活していないことで見る
            // An append after closing would recreate current/, so the check is that the file never comes back
            Assert.IsFalse(File.Exists(ProgressRecordPaths.CurrentEventsPath));
            Assert.IsFalse(ProgressRecordFiles.HasCurrentSession());

            var added = new List<string>(Directory.GetDirectories(GameSystemPaths.ProgressRecordOutboxDirectory));
            foreach (var directory in before) added.Remove(directory);
            Assert.AreEqual(1, added.Count);

            var record = JObject.Parse(File.ReadAllText(Path.Combine(added[0], ProgressRecordPaths.RecordFileName)));
            Assert.AreEqual("quit", (string)record["endReason"]);
            Assert.AreEqual(1, ((JArray)record["events"]).Count);
            Directory.Delete(added[0], true);
        }
    }
}
