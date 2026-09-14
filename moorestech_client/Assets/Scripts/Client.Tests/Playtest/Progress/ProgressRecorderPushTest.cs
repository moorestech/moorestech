using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Client.Game.Common;
using Client.Game.InGame.BugReport.Playtest;
using Client.Game.InGame.Playtest.Progress;
using Game.Paths;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

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
            ProgressTestSession.Clear();
        }

        // プッシュ経路は ctor で受けた依存を一切使わない。StartSession を呼ばない限り購読も張られない
        // The push path touches none of the ctor dependencies, and no subscription is made unless StartSession runs
        private static ProgressRecorder CreateRecorderForPushOnly()
        {
            return new ProgressRecorder(null, null, new EmptyPlaytestSessionIdentity());
        }

        [Test]
        public void クラフト要求のプッシュが1行のcraftRequestedになる()
        {
            var recorder = CreateRecorderForPushOnly();
            var recipeGuid = Guid.NewGuid();

            recorder.RecordCraftRequested(recipeGuid);

            var events = ProgressRecordFiles.ReadEvents(ProgressTestSession.Directory, out _);
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(ProgressEventType.CraftRequested, events[0].Type);
            Assert.AreEqual(recipeGuid.ToString(), ProgressEvents.ReadCraftRecipeGuid(events[0]));
        }

        [Test]
        public void 報告送信のプッシュが1行のreportSentになる()
        {
            var recorder = CreateRecorderForPushOnly();

            recorder.RecordReportSent(PlaytestReportKind.Feedback);

            var events = ProgressRecordFiles.ReadEvents(ProgressTestSession.Directory, out _);
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(ProgressEventType.ReportSent, events[0].Type);
            Assert.AreEqual(PlaytestReportKind.Feedback, ProgressEvents.ReadReportKind(events[0]));
        }

        [Test]
        public void プッシュ回数と行数が一致する()
        {
            var recorder = CreateRecorderForPushOnly();

            recorder.RecordCraftRequested(Guid.NewGuid());
            recorder.RecordReportSent(PlaytestReportKind.Bug);
            recorder.RecordCraftRequested(Guid.NewGuid());

            Assert.AreEqual(3, File.ReadAllLines(ProgressRecordPaths.EventsPathIn(ProgressTestSession.Directory)).Length);
        }

        [Test]
        public void 書き出し後のプッシュは記録されない()
        {
            var recorder = CreateRecorderForPushOnly();
            ProgressTestSession.WriteHeader(new ProgressRecordHeader { SessionStart = ProgressUtcTime.ToIso(DateTime.UtcNow) });
            recorder.RecordCraftRequested(Guid.NewGuid());
            var before = Directory.Exists(GameSystemPaths.ProgressRecordOutboxDirectory) ? Directory.GetDirectories(GameSystemPaths.ProgressRecordOutboxDirectory) : Array.Empty<string>();

            Assert.AreEqual(ShutdownFlushResult.Flushed, recorder.FlushOnShutdownAsync().GetAwaiter().GetResult());
            recorder.RecordReportSent(PlaytestReportKind.Bug);

            // 閉じた後の追記は current/ を作り直してしまうため、ファイルが復活していないことで見る
            // An append after closing would recreate current/, so the check is that the file never comes back
            Assert.IsFalse(File.Exists(ProgressRecordPaths.EventsPathIn(ProgressTestSession.Directory)));
            Assert.IsFalse(ProgressTestSession.HasCurrentSession());

            var added = new List<string>(Directory.GetDirectories(GameSystemPaths.ProgressRecordOutboxDirectory));
            foreach (var directory in before) added.Remove(directory);
            Assert.AreEqual(1, added.Count);

            var record = JObject.Parse(File.ReadAllText(Path.Combine(added[0], ProgressRecordPaths.RecordFileName)));
            Assert.AreEqual(ProgressEndReason.Quit, (string)record["endReason"]);
            Assert.AreEqual(1, ((JArray)record["events"]).Count);
            Directory.Delete(added[0], true);
        }

        // 書き出す中身が無いのに Flushed を返すと、記録が1件も出ていない起動が成功として流れる
        // Returning Flushed with nothing to write would let a boot that produced no record pass as a success
        [Test]
        public void 何も書かずに終了するとNothingFlushedを返す()
        {
            var recorder = CreateRecorderForPushOnly();

            LogAssert.Expect(LogType.Warning, new Regex("書き出せませんでした"));
            Assert.AreEqual(ShutdownFlushResult.NothingFlushed, recorder.FlushOnShutdownAsync().GetAwaiter().GetResult());
        }
    }
}
