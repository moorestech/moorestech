using System;
using System.IO;
using System.Linq;
using Client.Game.InGame.BugReport;
using Client.Game.InGame.Playtest.Progress;
using Client.Game.InGame.Playtest.Progress.Record;
using Client.Game.InGame.Playtest.Progress.Record.Events;
using Client.Game.InGame.Playtest.Progress.Storage;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Client.Tests.Playtest
{
    public class ProgressRecordFilesTest
    {
        [SetUp]
        [TearDown]
        public void ClearCurrent()
        {
            ProgressTestSession.Clear();
        }

        [Test]
        public void ヘッダとイベントを書いて閉じるとoutboxに箱が出てセッションの段が消える()
        {
            var start = DateTime.UtcNow;
            ProgressTestSession.WriteHeader(new ProgressRecordHeader { SessionStart = ProgressUtcTime.ToIso(start), WorldCreatedAt = "2026-09-10T09:00:00Z" });
            Assert.IsTrue(ProgressTestSession.HasCurrentSession());
            ProgressTestSession.AppendEvent(new BlockPlacedEvent(start, 1, 2));

            var bundle = ProgressRecordFiles.CloseCurrentInto(ProgressTestSession.Directory, ProgressEndReason.CrashRecovered, start.AddSeconds(30)).BundleDirectory;

            Assert.IsTrue(File.Exists(Path.Combine(bundle, ProgressRecordPaths.RecordFileName)));
            Assert.IsTrue(File.Exists(Path.Combine(bundle, BugReportOutbox.ReadyMarkerFileName)));
            var record = ReadRecord(bundle);
            Assert.AreEqual("crash-recovered", (string)record["endReason"]);
            Assert.AreEqual(2, (int)record["placedBlockCount"]);
            Assert.IsFalse(Directory.Exists(ProgressTestSession.Directory), "受け渡し印ごとセッションの段が消えていない");
            Directory.Delete(bundle, true);
        }

        [Test]
        // 「畳む中身が無い」を「書けなかった」と同じ結果にすると、終了コードが書き出し失敗として出てしまう
        // Reporting "nothing to fold" as "could not write" would surface the shutdown as a failed flush
        public void 閉じる中身が無いときは書き出し失敗ではなく中身無しを返す()
        {
            var result = ProgressRecordFiles.CloseCurrentInto(ProgressTestSession.Directory, ProgressEndReason.Quit, DateTime.UtcNow);

            Assert.IsNull(result.BundleDirectory);
            Assert.IsFalse(result.WriteFailed, "中身が無いだけなのに書き出し失敗として返っている");
        }

        // 壊れた行は捨てるが、捨てた件数は欠損として記録に載る（無音で消さない）
        // Broken lines are dropped, but how many were dropped reaches the record as a gap instead of vanishing
        [Test]
        public void 壊れたイベント行は飛ばして残りを読み件数を欠損に残す()
        {
            ProgressTestSession.WriteHeader(new ProgressRecordHeader { SessionStart = ProgressUtcTime.ToIso(DateTime.UtcNow) });
            ProgressTestSession.AppendEvent(new BlockPlacedEvent(DateTime.UtcNow, 1, 1));
            File.AppendAllText(ProgressRecordPaths.EventsPathIn(ProgressTestSession.Directory), "{ broken\n");
            ProgressTestSession.AppendEvent(new BlockPlacedEvent(DateTime.UtcNow, 2, 1));

            Assert.IsTrue(ProgressSessionContents.Load(ProgressTestSession.Directory, out var contents).Succeeded);
            Assert.AreEqual(2, contents.Events.Count);

            var bundle = ProgressRecordFiles.CloseCurrentInto(ProgressTestSession.Directory, ProgressEndReason.Quit, DateTime.UtcNow).BundleDirectory;
            CollectionAssert.Contains(MissingItems(ReadRecord(bundle)), ProgressRecordPaths.EventsFileName, "捨てた行が欠損として載っていない");
            Directory.Delete(bundle, true);
        }

        // 異常終了の残骸は終了時刻を持たない。代用した出所を名乗らないと、読み手は最後のイベント時刻を本物の終了時刻と読む（C19）
        // A crashed leftover has no end time; without declaring the substitute, a reader takes the last event's time for the real exit (C19)
        [Test]
        public void 残骸の終了時刻は最後のイベント時刻で代用しsessionEndの欠損を名乗る()
        {
            var lastEvent = DateTime.UtcNow.AddMinutes(-2);
            ProgressTestSession.WriteHeader(new ProgressRecordHeader { SessionStart = ProgressUtcTime.ToIso(lastEvent.AddMinutes(-3)) });
            ProgressTestSession.AppendEvent(new BlockPlacedEvent(lastEvent, 1, 1));

            var bundle = ProgressRecordFiles.CloseLeftoverInto(ProgressTestSession.Directory, ProgressEndReason.Quit, Array.Empty<MissingItem>()).BundleDirectory;

            var record = ReadRecord(bundle);
            Assert.AreEqual(ProgressUtcTime.ToIso(lastEvent), (string)record["sessionEnd"]);
            CollectionAssert.Contains(MissingItems(record), "sessionEnd", "代用した終了時刻の出所が欠損として載っていない");
            Directory.Delete(bundle, true);
        }

        internal static JObject ReadRecord(string bundle)
        {
            return JObject.Parse(File.ReadAllText(Path.Combine(bundle, ProgressRecordPaths.RecordFileName)));
        }

        internal static System.Collections.Generic.List<string> MissingItems(JObject record)
        {
            return ((JArray)record["missing"]).Select(item => (string)item["item"]).ToList();
        }
    }
}
