using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Client.Game.InGame.BugReport;
using Client.Game.InGame.BugReport.LastSession;
using Client.Game.InGame.BugReport.Playtest;
using Game.Paths;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Client.Tests.BugReport
{
    // 前回異常終了の退避物から kind=crash の箱が1つ出来ることを押さえる
    // Pins that one kind=crash box comes out of the previous session's salvaged files
    public class CrashBundleWriterTest
    {
        // WriteAsyncはスレッドプールからPlayerLoopで戻るが、CIのバッチモードEditModeではPlayerLoopが回らず戻れない。本体のWriteを同期で呼ぶ
        // WriteAsync returns from the thread pool via the PlayerLoop, which CI's batch-mode EditMode never pumps, so the synchronous Write core is called

        // スナップショットとパケットログとダンプまで通しで置く。接頭辞の代入先を入れ替えても緑になるテストにしない
        // Lays down snapshots, packet logs and a dump end to end, so swapping the prefixes' destinations can no longer stay green
        [Test]
        public void 退避物と説明文からcrashの箱を書く()
        {
            var source = Path.Combine(Path.GetTempPath(), $"moorestech-crash-{Guid.NewGuid():N}");
            var recording = Path.Combine(source, "recording");
            Directory.CreateDirectory(recording);
            File.WriteAllText(Path.Combine(recording, "seg_00.mp4"), "video");
            var playerLog = Path.Combine(source, "Player-prev.log");
            File.WriteAllText(playerLog, "log");

            // 退避は入れ子を保ったまま移すため、入れ子の中のスナップショットも分類されないと再現側が読み落とす
            // The salvage keeps its nesting, so a snapshot inside a subdirectory must be classified too or the reproduction misses it
            var snapshots = Path.Combine(source, "snapshots");
            var nested = Path.Combine(snapshots, "pid_4321");
            Directory.CreateDirectory(nested);
            File.WriteAllText(Path.Combine(snapshots, "tick_600.json"), "{}");
            File.WriteAllText(Path.Combine(snapshots, "packets_601.bin"), "packets");
            File.WriteAllText(Path.Combine(nested, "tick_602.json"), "{}");

            var dump = Path.Combine(source, "moorestech-2026-09-14.ips");
            File.WriteAllText(dump, "dump");

            var artifacts = TestPreviousSessionArtifacts.Unclean(TestPreviousSessionArtifacts.UnusedLastSessionDirectory, recording, snapshots, playerLog, new List<string> { dump });

            var bundle = WriteBundle(artifacts, "落ちた");

            try
            {
                var manifest = JObject.Parse(File.ReadAllText(Path.Combine(bundle, BugReportBundleLayout.ManifestFileName)));
                BundleManifestContract.AssertPlanC(bundle, manifest);
                Assert.AreEqual("crash", (string)manifest["kind"]);
                Assert.AreEqual("落ちた", (string)manifest["description"]);
                Assert.IsTrue(File.Exists(Path.Combine(bundle, BugReportBundleLayout.RecordingDirectoryName, "seg_00.mp4")));
                Assert.IsTrue(File.Exists(Path.Combine(bundle, BugReportBundleLayout.LogsDirectoryName, "Player-prev.log")));

                var snapshotDirectory = Path.Combine(bundle, BugReportBundleLayout.SnapshotDirectoryName);
                Assert.IsTrue(File.Exists(Path.Combine(snapshotDirectory, "tick_600.json")), "スナップショットが snapshots/ へ移っていない");
                Assert.IsTrue(File.Exists(Path.Combine(snapshotDirectory, "packets_601.bin")), "パケットログが snapshots/ へ移っていない");
                Assert.IsTrue(File.Exists(Path.Combine(snapshotDirectory, "pid_4321", "tick_602.json")), "入れ子のスナップショットが落ちている");

                var snapshotFiles = BundleManifestContract.StringList(manifest, "snapshotFiles");
                var packetLogFiles = BundleManifestContract.StringList(manifest, "packetLogFiles");
                CollectionAssert.Contains(snapshotFiles, "tick_600.json", "tick_ がsnapshotFilesへ分類されていない");
                CollectionAssert.Contains(snapshotFiles, Path.Combine("pid_4321", "tick_602.json"), "入れ子のtick_がsnapshotFilesへ分類されていない");
                CollectionAssert.DoesNotContain(snapshotFiles, "packets_601.bin", "packets_ がsnapshotFilesへ混ざっている");
                CollectionAssert.Contains(packetLogFiles, "packets_601.bin", "packets_ がpacketLogFilesへ分類されていない");
                CollectionAssert.DoesNotContain(packetLogFiles, "tick_600.json", "tick_ がpacketLogFilesへ混ざっている");

                Assert.IsTrue(File.Exists(Path.Combine(bundle, BugReportBundleLayout.CrashDumpsDirectoryName, "moorestech-2026-09-14.ips")), "クラッシュダンプが crashDumps/ へ入っていない");
            }
            finally
            {
                Directory.Delete(bundle, true);
                Directory.Delete(source, true);
            }
        }

        // 退避は pid_<PID>/ の入れ子を保ったまま移すため、箱への写しも入れ子を辿らないと録画が丸ごと落ちる
        // The salvage preserves the pid_<PID>/ nesting, so a non-recursive copy would drop the whole recording
        [Test]
        public void 入れ子のまま退避された録画も箱へ入る()
        {
            var source = Path.Combine(Path.GetTempPath(), $"moorestech-crash-{Guid.NewGuid():N}");
            var recording = Path.Combine(source, "recording", "pid_1234");
            Directory.CreateDirectory(recording);
            File.WriteAllText(Path.Combine(recording, "segment-0.mp4"), "video");

            var artifacts = TestPreviousSessionArtifacts.UncleanWithRecording(Path.Combine(source, "recording"));

            var bundle = WriteBundle(artifacts, "入れ子");

            try
            {
                Assert.IsTrue(File.Exists(Path.Combine(bundle, BugReportBundleLayout.RecordingDirectoryName, "pid_1234", "segment-0.mp4")));
            }
            finally
            {
                Directory.Delete(bundle, true);
                Directory.Delete(source, true);
            }
        }

        [Test]
        public void 退避物が空でも説明文だけで箱になり欠損が残る()
        {
            var artifacts = TestPreviousSessionArtifacts.Unclean();
            artifacts.Missing.Add(new MissingItem { Item = "recording", Reason = "退避元が空" });

            var bundle = WriteBundle(artifacts, "起動しない");

            try
            {
                var manifest = JObject.Parse(File.ReadAllText(Path.Combine(bundle, BugReportBundleLayout.ManifestFileName)));
                Assert.AreEqual("crash", (string)manifest["kind"]);
                Assert.IsTrue(((JArray)manifest["missing"]).Any(item => (string)item["item"] == "recording" && (string)item["reason"] == "退避元が空"), "退避で積んだ欠損がmanifestに残っていない");
            }
            finally
            {
                Directory.Delete(bundle, true);
            }
        }

        // ディスク由来の失敗（読み取り不能）は項目ごとに隔離され、他の退避物・manifest・READYの書き出しは続く
        // A disk-originated failure (unreadable source) is isolated per item; the other artifacts, manifest and READY still get written
        [Test]
        public void 退避物の読み取り不能はmissingへ隔離され他の項目とREADYは書かれる()
        {
            var source = Path.Combine(Path.GetTempPath(), $"moorestech-crash-{Guid.NewGuid():N}");
            var recording = Path.Combine(source, "recording");
            Directory.CreateDirectory(recording);
            File.WriteAllText(Path.Combine(recording, "seg_00.mp4"), "video");
            var playerLog = Path.Combine(source, "Player-prev.log");
            File.WriteAllText(playerLog, "log");
            Chmod(playerLog, "000");

            var artifacts = TestPreviousSessionArtifacts.Unclean(TestPreviousSessionArtifacts.UnusedLastSessionDirectory, recording, null, playerLog, new List<string>());

            string bundle = null;
            try
            {
                bundle = WriteBundle(artifacts, "読めない");

                Assert.IsNotNull(bundle, "1項目の失敗でゲート応答処理まで例外が伝播せず箱自体は書けること");
                Assert.IsTrue(File.Exists(Path.Combine(bundle, BugReportOutbox.ReadyMarkerFileName)));
                Assert.IsTrue(File.Exists(Path.Combine(bundle, BugReportBundleLayout.RecordingDirectoryName, "seg_00.mp4")), "読めない項目とは別の退避物は隔離の巻き添えにならない");
                Assert.IsFalse(File.Exists(Path.Combine(bundle, BugReportBundleLayout.LogsDirectoryName, "Player-prev.log")));

                var manifest = JObject.Parse(File.ReadAllText(Path.Combine(bundle, BugReportBundleLayout.ManifestFileName)));
                var missing = (JArray)manifest["missing"];
                Assert.IsTrue(missing.Any(item => (string)item["item"] == BugReportBundleLayout.LogsDirectoryName), "失敗した項目名と理由がmanifest.missingへ残ること");
            }
            finally
            {
                Chmod(playerLog, "600");
                if (bundle != null) Directory.Delete(bundle, true);
                Directory.Delete(source, true);
            }
        }

        // テスト専用: chmodでディスクの読み取り失敗（IsDiskFailure対象のUnauthorizedAccessException）を確実に再現する
        // Test-only: chmod deterministically reproduces the disk read failure (an UnauthorizedAccessException IsDiskFailure catches)
        private static void Chmod(string path, string mode)
        {
            using var process = Process.Start(new ProcessStartInfo("chmod", $"{mode} \"{path}\"") { UseShellExecute = false });
            process.WaitForExit();
        }

        private static string WriteBundle(PreviousSessionArtifacts artifacts, string description)
        {
            return CrashBundleWriter.Write(artifacts, description, RepositoryStateProbe.RepositoryRoot, RepositoryStateProbe.MasterDataRoot);
        }
    }
}
