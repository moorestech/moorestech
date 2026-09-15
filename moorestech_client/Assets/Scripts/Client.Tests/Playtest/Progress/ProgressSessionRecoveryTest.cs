using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Client.Game.InGame.BugReport;
using Client.Game.InGame.Playtest.Progress;
using Client.Game.InGame.Playtest.Progress.Record;
using Client.Game.InGame.Playtest.Progress.Record.Events;
using Client.Game.InGame.Playtest.Progress.Storage;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.Playtest
{
    public class ProgressSessionRecoveryTest
    {
        private const int CurrentProcessId = 500;
        private static readonly IReadOnlyList<MissingItem> NoSkipped = Array.Empty<MissingItem>();

        [SetUp]
        [TearDown]
        public void ClearCurrent()
        {
            ProgressTestSession.Clear();
        }

        private static void WriteLeftoverSession()
        {
            ProgressTestSession.WriteHeader(new ProgressRecordHeader { SessionStart = ProgressUtcTime.ToIso(DateTime.UtcNow.AddMinutes(-5)), WorldCreatedAt = "2026-09-10T09:00:00Z" });
            ProgressTestSession.AppendEvent(new BlockPlacedEvent(DateTime.UtcNow.AddMinutes(-4), 1, 1));
        }

        private static string Recover(ProgressEndReason endReason)
        {
            return ProgressRecordFiles.CloseLeftoverInto(ProgressTestSession.Directory, endReason, NoSkipped).BundleDirectory;
        }

        [Test]
        public void 残骸が無ければ何もしない()
        {
            Assert.IsNull(Recover(ProgressEndReason.Quit));
            Assert.IsNull(Recover(ProgressEndReason.CrashRecovered));
        }

        [Test]
        public void 異常終了の残骸はcrash_recoveredで送る()
        {
            WriteLeftoverSession();
            var bundle = Recover(ProgressEndReason.CrashRecovered);
            Assert.AreEqual("crash-recovered", (string)ProgressRecordFilesTest.ReadRecord(bundle)["endReason"]);
            Assert.IsFalse(ProgressTestSession.HasCurrentSession());
            Directory.Delete(bundle, true);
        }

        [Test]
        public void 正常終了なのに残った残骸はquitで送る()
        {
            // マーカーは書けたが record を書き切る前に落ちた場合。恒久的に残さず必ず回収する
            // The marker was written but the record was not; this leftover is always recovered, never left behind
            WriteLeftoverSession();
            var bundle = Recover(ProgressEndReason.Quit);
            Assert.AreEqual("quit", (string)ProgressRecordFilesTest.ReadRecord(bundle)["endReason"]);
            Assert.IsFalse(ProgressTestSession.HasCurrentSession());
            Directory.Delete(bundle, true);
        }

        // ヘッダ書き込み前に落ちた残骸。回収されないと次のセッションの記録へ黙って混ざる
        // A leftover from a crash before the header was written; if not recovered it silently mixes into the next session's record
        [Test]
        public void ヘッダ無しでイベントだけの残骸も回収されcurrentが空になる()
        {
            ProgressTestSession.AppendEvent(new CraftCompletedEvent(DateTime.UtcNow.AddMinutes(-3), 1, Guid.NewGuid().ToString()));

            LogAssert.Expect(LogType.Warning, new Regex("ヘッダが無い"));
            var bundle = Recover(ProgressEndReason.CrashRecovered);

            var record = ProgressRecordFilesTest.ReadRecord(bundle);
            CollectionAssert.Contains(ProgressRecordFilesTest.MissingItems(record), "header", "ヘッダ欠損が欠損列に載っていない");
            Assert.AreEqual(1, record["events"].Count());
            Assert.AreEqual(1, (int)record["craftCount"]);
            Assert.IsFalse(ProgressTestSession.HasCurrentSession());
            Directory.Delete(bundle, true);
        }

        [Test]
        public void ヘッダ無しの残骸のイベントは次のセッションの記録に混ざらない()
        {
            ProgressTestSession.AppendEvent(new CraftCompletedEvent(DateTime.UtcNow.AddMinutes(-3), 1, Guid.NewGuid().ToString()));
            LogAssert.Expect(LogType.Warning, new Regex("ヘッダが無い"));
            var recovered = Recover(ProgressEndReason.CrashRecovered);

            // 回収→current/ を空にする→新セッション開始、の順序でしか新しいヘッダは書かれない
            // A new header is written only in this order: recover, empty current/, then start the new session
            var writer = new ProgressSessionWriter();
            writer.WriteHeader(new ProgressRecordHeader { SessionStart = ProgressUtcTime.ToIso(DateTime.UtcNow) });
            writer.Append(new BlockPlacedEvent(DateTime.UtcNow, 2, 1));
            var bundle = writer.Close(ProgressEndReason.Quit, DateTime.UtcNow).BundleDirectory;

            var record = ProgressRecordFilesTest.ReadRecord(bundle);
            Assert.AreEqual(1, record["events"].Count());
            Assert.AreEqual(BlockPlacedEvent.TypeName, (string)record["events"][0]["type"]);
            Assert.AreEqual(0, (int)record["craftCount"]);
            CollectionAssert.DoesNotContain(ProgressRecordFilesTest.MissingItems(record), "header");
            Directory.Delete(recovered, true);
            Directory.Delete(bundle, true);
        }

        [Test]
        public void イベントが0件の残骸も送れる()
        {
            ProgressTestSession.WriteHeader(new ProgressRecordHeader { SessionStart = ProgressUtcTime.ToIso(DateTime.UtcNow.AddMinutes(-1)) });
            var bundle = Recover(ProgressEndReason.CrashRecovered);
            Assert.AreEqual(0, ProgressRecordFilesTest.ReadRecord(bundle)["events"].Count());
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
            var scannedPaths = scan.Sessions.Select(session => session.Path).ToList();

            CollectionAssert.DoesNotContain(scannedPaths, livePidDirectory, "生存pidの残骸を畳もうとしている");
            CollectionAssert.Contains(scannedPaths, ProgressTestSession.Directory, "自分のpidの残骸が回収対象に入っていない");
            Assert.IsTrue(scan.Skipped.Any(item => item.Reason.Contains("pid 1")), "触らなかった理由が残っていない");
            Directory.Delete(livePidDirectory, true);
        }

        // 終わり方はpidごとに印の消費結果で決まる。1つのboolを全残骸へ当てると、正常終了したpidの残骸まで crash-recovered になる（C36）
        // The ending is decided per pid from the consumed marks; one bool for every leftover would turn a cleanly exited pid's leftover into crash-recovered too (C36)
        [Test]
        public void 終了理由は残骸のpidごとに決まる()
        {
            var exitedCleanly = new Dictionary<int, bool> { [700] = true, [701] = false };
            var missing = new List<MissingItem>();

            Assert.AreEqual(ProgressEndReason.Quit, ProgressSessionRecovery.ResolveEndReason(700, CurrentProcessId, exitedCleanly, missing));
            Assert.AreEqual(ProgressEndReason.CrashRecovered, ProgressSessionRecovery.ResolveEndReason(701, CurrentProcessId, exitedCleanly, missing));
            Assert.IsEmpty(missing, "印で終わり方が分かっているpidに欠損を名乗っている");
        }

        // 印の無いpidと自pidは終わり方が分からない。crash-recovered に倒し、分からなかった理由を欠損として名乗る
        // A pid without marks and this pid cannot tell how they ended; they fall to crash-recovered with the reason declared as a gap
        [Test]
        public void 終わり方が分からない残骸はcrash_recoveredで理由を名乗る()
        {
            var exitedCleanly = new Dictionary<int, bool> { [CurrentProcessId] = true };

            var ownMissing = new List<MissingItem>();
            LogAssert.Expect(LogType.Warning, new Regex("自プロセス"));
            Assert.AreEqual(ProgressEndReason.CrashRecovered, ProgressSessionRecovery.ResolveEndReason(CurrentProcessId, CurrentProcessId, exitedCleanly, ownMissing));
            Assert.AreEqual("endReason", ownMissing.Single().Item);

            var unknownMissing = new List<MissingItem>();
            LogAssert.Expect(LogType.Warning, new Regex("印を消費していない"));
            Assert.AreEqual(ProgressEndReason.CrashRecovered, ProgressSessionRecovery.ResolveEndReason(702, CurrentProcessId, exitedCleanly, unknownMissing));
            Assert.AreEqual("endReason", unknownMissing.Single().Item);
        }
    }
}
