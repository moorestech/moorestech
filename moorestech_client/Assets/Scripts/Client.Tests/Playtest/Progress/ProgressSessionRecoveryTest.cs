using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Client.Game.InGame.BugReport;
using Client.Game.InGame.Playtest.Progress;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.Playtest
{
    public class ProgressSessionRecoveryTest
    {
        private static readonly IReadOnlyList<MissingItem> NoSkipped = Array.Empty<MissingItem>();

        [SetUp]
        [TearDown]
        public void ClearCurrent()
        {
            ProgressTestSession.Clear();
        }

        private static void WriteLeftoverSession()
        {
            ProgressTestSession.WriteHeader(new ProgressRecordHeader
            {
                SessionStart = ProgressUtcTime.ToIso(DateTime.UtcNow.AddMinutes(-5)),
                WorldCreatedAt = "2026-09-10T09:00:00Z",
            });
            ProgressTestSession.AppendEvent(ProgressEvents.BlockPlaced(DateTime.UtcNow.AddMinutes(-4), 1, 1));
        }

        private static string Recover(bool previousExitWasClean)
        {
            return ProgressSessionRecovery.RecoverLeftoverSession(ProgressTestSession.Directory, previousExitWasClean, NoSkipped);
        }

        [Test]
        public void 残骸が無ければ何もしない()
        {
            Assert.IsNull(Recover(true));
            Assert.IsNull(Recover(false));
        }

        [Test]
        public void 異常終了の残骸はcrash_recoveredで送る()
        {
            WriteLeftoverSession();
            var bundle = Recover(false);
            var record = JObject.Parse(File.ReadAllText(Path.Combine(bundle, ProgressRecordPaths.RecordFileName)));
            Assert.AreEqual(ProgressEndReason.CrashRecovered, (string)record["endReason"]);
            Assert.IsFalse(ProgressTestSession.HasCurrentSession());
            Directory.Delete(bundle, true);
        }

        [Test]
        public void 正常終了なのに残った残骸はquitで送る()
        {
            // マーカーは書けたが record を書き切る前に落ちた場合。恒久的に残さず必ず回収する
            // The marker was written but the record was not; this leftover is always recovered, never left behind
            WriteLeftoverSession();
            var bundle = Recover(true);
            var record = JObject.Parse(File.ReadAllText(Path.Combine(bundle, ProgressRecordPaths.RecordFileName)));
            Assert.AreEqual(ProgressEndReason.Quit, (string)record["endReason"]);
            Assert.IsFalse(ProgressTestSession.HasCurrentSession());
            Directory.Delete(bundle, true);
        }

        // ヘッダ書き込み前に落ちた残骸。回収されないと次のセッションの記録へ黙って混ざる
        // A leftover from a crash before the header was written; if not recovered it silently mixes into the next session's record
        [Test]
        public void ヘッダ無しでイベントだけの残骸も回収されcurrentが空になる()
        {
            ProgressTestSession.AppendEvent(ProgressEvents.CraftRequested(DateTime.UtcNow.AddMinutes(-3), 1, Guid.NewGuid()));

            LogAssert.Expect(LogType.Warning, new Regex("ヘッダが無い"));
            var bundle = Recover(false);

            var record = JObject.Parse(File.ReadAllText(Path.Combine(bundle, ProgressRecordPaths.RecordFileName)));
            CollectionAssert.Contains(MissingItems(record), "header", "ヘッダ欠損が欠損列に載っていない");
            Assert.AreEqual(1, ((JArray)record["events"]).Count);
            Assert.AreEqual(ProgressEndReason.CrashRecovered, (string)record["endReason"]);
            Assert.IsFalse(ProgressTestSession.HasCurrentSession());
            Assert.IsFalse(File.Exists(ProgressRecordPaths.EventsPathIn(ProgressTestSession.Directory)));
            Directory.Delete(bundle, true);
        }

        [Test]
        public void ヘッダ無しの残骸のイベントは次のセッションの記録に混ざらない()
        {
            ProgressTestSession.AppendEvent(ProgressEvents.CraftRequested(DateTime.UtcNow.AddMinutes(-3), 1, Guid.NewGuid()));
            LogAssert.Expect(LogType.Warning, new Regex("ヘッダが無い"));
            var recovered = Recover(false);

            // 回収→current/ を空にする→新セッション開始、の順序でしか新しいヘッダは書かれない
            // A new header is written only in this order: recover, empty current/, then start the new session
            var writer = new ProgressSessionWriter();
            writer.WriteHeader(new ProgressRecordHeader { SessionStart = ProgressUtcTime.ToIso(DateTime.UtcNow) });
            writer.Append(ProgressEvents.BlockPlaced(DateTime.UtcNow, 2, 1));
            var bundle = writer.Close(ProgressEndReason.Quit, DateTime.UtcNow);

            var record = JObject.Parse(File.ReadAllText(Path.Combine(bundle, ProgressRecordPaths.RecordFileName)));
            var events = (JArray)record["events"];
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(ProgressEventType.BlockPlaced, (string)events[0]["type"]);
            Assert.AreEqual(0, (int)record["craftCount"]);
            CollectionAssert.DoesNotContain(MissingItems(record), "header");
            Directory.Delete(recovered, true);
            Directory.Delete(bundle, true);
        }

        [Test]
        public void イベントが0件の残骸も送れる()
        {
            ProgressTestSession.WriteHeader(new ProgressRecordHeader { SessionStart = ProgressUtcTime.ToIso(DateTime.UtcNow.AddMinutes(-1)) });
            var bundle = Recover(false);
            var record = JObject.Parse(File.ReadAllText(Path.Combine(bundle, ProgressRecordPaths.RecordFileName)));
            Assert.AreEqual(0, ((JArray)record["events"]).Count);
            Directory.Delete(bundle, true);
        }

        // 生存している他プロセスの残骸には触らない。触ると並列起動したセッションのイベントが1本に混ざる
        // A live process's leftover is never touched; touching it would mix a parallel session's events into one record
        [Test]
        public void 生存プロセスのcurrentは回収対象に入らず理由が残る()
        {
            // pid 1 は必ず生きている（init/launchd）。その残骸を畳むと並列起動のセッションを横取りしたのと同じことになる
            // pid 1 is always alive (init/launchd); folding its leftover is exactly what stealing a parallel session looks like
            var livePidDirectory = ProgressCurrentSession.DirectoryFor(1);
            ProgressRecordFiles.WriteHeader(livePidDirectory, new ProgressRecordHeader { SessionStart = ProgressUtcTime.ToIso(DateTime.UtcNow) });
            ProgressTestSession.WriteHeader(new ProgressRecordHeader { SessionStart = ProgressUtcTime.ToIso(DateTime.UtcNow) });

            LogAssert.Expect(LogType.Warning, new Regex("pid 1 は実行中"));
            var scan = ProgressCurrentSession.ScanLeftovers();

            CollectionAssert.DoesNotContain(scan.Directories, livePidDirectory, "生存pidの残骸を畳もうとしている");
            CollectionAssert.Contains(scan.Directories, ProgressTestSession.Directory, "自分のpidの残骸が回収対象に入っていない");
            Assert.IsTrue(scan.Skipped.Any(item => item.Reason.Contains("pid 1")), "触らなかった理由が残っていない");
            Directory.Delete(livePidDirectory, true);
        }

        private static List<string> MissingItems(JObject record)
        {
            return ((JArray)record["missing"]).Select(item => (string)item["item"]).ToList();
        }
    }
}
