using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Client.Game.InGame.BugReport;
using Client.Game.InGame.Playtest.Progress;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Client.Tests.Playtest
{
    public class ProgressRecordFilesTest
    {
        private static readonly IReadOnlyList<MissingItem> NoExtraMissing = Array.Empty<MissingItem>();

        [SetUp]
        [TearDown]
        public void ClearCurrent()
        {
            ProgressTestSession.Clear();
        }

        [Test]
        public void ヘッダとイベントを書いて閉じるとoutboxに箱が出る()
        {
            var start = DateTime.UtcNow;
            ProgressTestSession.WriteHeader(new ProgressRecordHeader
            {
                SessionStart = ProgressUtcTime.ToIso(start),
                WorldCreatedAt = "2026-09-10T09:00:00Z",
                SteamId = "",
                TotalPlaySecondsAtStart = 0,
            });
            Assert.IsTrue(ProgressTestSession.HasCurrentSession());

            ProgressTestSession.AppendEvent(ProgressEvents.BlockPlaced(start, 1, 2));
            Assert.AreEqual(1, ProgressRecordFiles.ReadEvents(ProgressTestSession.Directory, out _).Count);

            var bundle = ProgressRecordFiles.CloseCurrentInto(ProgressTestSession.Directory, ProgressEndReason.CrashRecovered, start.AddSeconds(30), NoExtraMissing).BundleDirectory;

            Assert.IsTrue(File.Exists(Path.Combine(bundle, ProgressRecordPaths.RecordFileName)));
            Assert.IsTrue(File.Exists(Path.Combine(bundle, BugReportOutbox.ReadyMarkerFileName)));
            var record = JObject.Parse(File.ReadAllText(Path.Combine(bundle, ProgressRecordPaths.RecordFileName)));
            Assert.AreEqual(ProgressEndReason.CrashRecovered, (string)record["endReason"]);
            Assert.AreEqual(2, (int)record["placedBlockCount"]);
            Assert.IsFalse(ProgressTestSession.HasCurrentSession());
            Directory.Delete(bundle, true);
        }

        [Test]
        // 「畳む中身が無い」を「書けなかった」と同じ結果にすると、終了コードが書き出し失敗として出てしまう
        // Reporting "nothing to fold" as "could not write" would surface the shutdown as a failed flush
        public void 閉じる中身が無いときは書き出し失敗ではなく中身無しを返す()
        {
            var result = ProgressRecordFiles.CloseCurrentInto(ProgressTestSession.Directory, ProgressEndReason.Quit, DateTime.UtcNow, NoExtraMissing);

            Assert.IsNull(result.BundleDirectory);
            Assert.IsFalse(result.WriteFailed, "中身が無いだけなのに書き出し失敗として返っている");
        }

        // 壊れた行は捨てるが、捨てた件数は欠損として記録に載る（無音で消さない）
        // Broken lines are dropped, but how many were dropped reaches the record as a gap instead of vanishing
        [Test]
        public void 壊れたイベント行は飛ばして残りを読み件数を欠損に残す()
        {
            ProgressTestSession.WriteHeader(new ProgressRecordHeader { SessionStart = ProgressUtcTime.ToIso(DateTime.UtcNow) });
            ProgressTestSession.AppendEvent(ProgressEvents.BlockPlaced(DateTime.UtcNow, 1, 1));
            File.AppendAllText(ProgressRecordPaths.EventsPathIn(ProgressTestSession.Directory), "{ broken\n");
            ProgressTestSession.AppendEvent(ProgressEvents.BlockPlaced(DateTime.UtcNow, 2, 1));

            var events = ProgressRecordFiles.ReadEvents(ProgressTestSession.Directory, out var brokenLineCount);
            Assert.AreEqual(2, events.Count);
            Assert.AreEqual(1, brokenLineCount);

            var bundle = ProgressRecordFiles.CloseCurrentInto(ProgressTestSession.Directory, ProgressEndReason.Quit, DateTime.UtcNow, NoExtraMissing).BundleDirectory;
            var record = JObject.Parse(File.ReadAllText(Path.Combine(bundle, ProgressRecordPaths.RecordFileName)));
            var missingItems = ((JArray)record["missing"]).Select(item => (string)item["item"]).ToList();
            CollectionAssert.Contains(missingItems, ProgressRecordPaths.EventsFileName, "捨てた行が欠損として載っていない");
            Directory.Delete(bundle, true);
        }
    }
}
