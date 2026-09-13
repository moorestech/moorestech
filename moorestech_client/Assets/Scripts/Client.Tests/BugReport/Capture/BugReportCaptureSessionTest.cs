using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Client.Game.InGame.BugReport;
using Client.Game.InGame.BugReport.Capture;
using Client.Game.InGame.BugReport.Recording;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.BugReport.Capture
{
    public class BugReportCaptureSessionTest
    {
        [Test]
        public void 開始で確保中になり完了イベントで解除される()
        {
            var sources = new FakeBugReportCaptureSources();
            var session = new BugReportCaptureSession(sources);
            session.BeginOnPauseMenu();
            Assert.AreEqual(BugReportCaptureStatus.Capturing, session.Status.Value.Kind);
            Assert.AreEqual(1, sources.TakeRecordingCount);

            session.OnServerCaptureCompleted(new ServerCaptureCompletion(7, 5, true, "/w/snapshots", "/master/server_v8", new List<string> { "tick_5.json" }, new List<string> { "packets_6.bin" }, null, 0));

            Assert.AreEqual(BugReportCaptureStatus.Ready, session.Status.Value.Kind);
            var data = session.TryBeginSubmit().Data;
            Assert.AreEqual(5UL, data.ReportTick);

            // 送信時に読むのは退避先。サーバーの置き場を直接読むと記入中の剪定で実体が消える
            // A send reads the staged copies; reading the server's directory would find them pruned away while typing
            Assert.AreEqual("/tmp/staging", data.StagedSnapshotDirectory);
            Assert.AreEqual("/w/snapshots", sources.StagedFromDirectory);
            Assert.AreEqual("/w", data.WorldRootDirectory);

            // 記録時のサーバーデータを取り込み損ねると、再現側が別マスタで再生して読み解けない例外で落ちる
            // Losing the recording's server data makes the reproduction replay different masters and die with an unreadable exception
            Assert.AreEqual("/master/server_v8", data.ServerDataDirectory);
            CollectionAssert.AreEqual(new[] { "tick_5.json" }, data.SnapshotFileNames);
            CollectionAssert.AreEqual(new[] { "packets_6.bin" }, data.PacketLogFileNames);
            CollectionAssert.AreEqual(new[] { "/tmp/seg_00.mp4" }, data.VideoSegmentFiles);
            Assert.AreEqual("/tmp/shot.png", data.ScreenshotPath);
            Assert.AreEqual(0, data.Missing.Count);
        }

        [Test]
        public void 別の要求IDの完了は無視する()
        {
            var sources = new FakeBugReportCaptureSources();
            var session = new BugReportCaptureSession(sources);
            session.BeginOnPauseMenu();

            session.OnServerCaptureCompleted(new ServerCaptureCompletion(99, 5, true, "/w/snapshots", "/master/server_v8", new List<string>(), new List<string>(), null, 0));

            Assert.AreEqual(BugReportCaptureStatus.Capturing, session.Status.Value.Kind);
            Assert.IsNull(sources.StagedFromDirectory);
        }

        [Test]
        public void サーバーが要求を拒否したら待たずに欠損へ載せる()
        {
            var sources = new FakeBugReportCaptureSources { RequestResult = new BugReportServerCaptureRequest(false, 0, "常時記録が無効") };
            var session = new BugReportCaptureSession(sources);

            LogAssert.Expect(LogType.Warning, new Regex("serverSnapshot"));
            session.BeginOnPauseMenu();

            Assert.AreEqual(BugReportCaptureStatus.Ready, session.Status.Value.Kind);
            CollectionAssert.Contains(session.Status.Value.Missing, "serverSnapshot");
            Assert.IsTrue(session.TryBeginSubmit().Data.Missing.Single(missing => missing.Item == "serverSnapshot").Reason.Contains("常時記録が無効"));
        }

        [Test]
        public void 拒否されたあとに来た完了イベントは取り込まない()
        {
            var sources = new FakeBugReportCaptureSources { RequestResult = new BugReportServerCaptureRequest(false, 0, "常時記録が無効") };
            var session = new BugReportCaptureSession(sources);
            LogAssert.Expect(LogType.Warning, new Regex("serverSnapshot"));
            session.BeginOnPauseMenu();

            session.OnServerCaptureCompleted(new ServerCaptureCompletion(0, 9, true, "/w/snapshots", "/master/server_v8", new List<string> { "tick_9.json" }, new List<string>(), null, 0));

            Assert.IsNull(sources.StagedFromDirectory);
        }

        [Test]
        public void サーバーの書き出し失敗は欠損へ載せて待ちを打ち切る()
        {
            var sources = new FakeBugReportCaptureSources();
            var session = new BugReportCaptureSession(sources);
            session.BeginOnPauseMenu();

            LogAssert.Expect(LogType.Warning, new Regex("serverSnapshot"));
            session.OnServerCaptureCompleted(new ServerCaptureCompletion(7, 5, false, "/w/snapshots", "/master/server_v8", new List<string> { "tick_5.json" }, new List<string>(), null, 0));

            Assert.AreEqual(BugReportCaptureStatus.Ready, session.Status.Value.Kind);
            CollectionAssert.Contains(session.Status.Value.Missing, "serverSnapshot");
            Assert.IsNull(sources.StagedFromDirectory);
        }

        [Test]
        public void 完了イベントが来ないままタイムアウトすると待ちを打ち切る()
        {
            var sources = new FakeBugReportCaptureSources();
            var session = new BugReportCaptureSession(sources);
            session.BeginOnPauseMenu();
            Assert.AreEqual(BugReportCaptureStatus.Capturing, session.Status.Value.Kind);

            LogAssert.Expect(LogType.Warning, new Regex("serverSnapshot"));
            sources.ElapseServerCaptureTimeout();

            Assert.AreEqual(BugReportCaptureStatus.Ready, session.Status.Value.Kind);
            CollectionAssert.Contains(session.Status.Value.Missing, "serverSnapshot");
        }

        [Test]
        public void タイムアウトが来ても取り込み済みなら欠損にしない()
        {
            var sources = new FakeBugReportCaptureSources();
            var session = new BugReportCaptureSession(sources);
            session.BeginOnPauseMenu();
            session.OnServerCaptureCompleted(new ServerCaptureCompletion(7, 5, true, "/w/snapshots", "/master/server_v8", new List<string>(), new List<string>(), null, 0));

            sources.ElapseServerCaptureTimeout();

            var data = session.TryBeginSubmit().Data;
            Assert.AreEqual(0, data.Missing.Count);
            Assert.AreEqual("/tmp/staging", data.StagedSnapshotDirectory);
        }

        [Test]
        public void 録画が無効なら欠損に載る()
        {
            var sources = new FakeBugReportCaptureSources { Recording = CapturedRecording.Unavailable("ffmpeg なし") };
            var session = new BugReportCaptureSession(sources);

            LogAssert.Expect(LogType.Warning, new Regex("video"));
            session.BeginOnPauseMenu();

            CollectionAssert.Contains(session.Status.Value.Missing, "video");
            Assert.AreEqual(1, sources.TakeRecordingCount);
        }

        // 縮退の申告を握り潰すと、区間は揃っているのにパケットだけ欠けた報告が「完全」に見える
        // Swallowing the declaration makes a report with complete segments but no packets look complete
        [Test]
        public void サーバーが申告したパケット記録の縮退が欠損に載る()
        {
            var sources = new FakeBugReportCaptureSources();
            var session = new BugReportCaptureSession(sources);
            session.BeginOnPauseMenu();

            LogAssert.Expect(LogType.Warning, new Regex("packetLog"));
            session.OnServerCaptureCompleted(new ServerCaptureCompletion(7, 5, true, "/w/snapshots", "/master/server_v8", new List<string> { "tick_5.json" }, new List<string>(), "書き込みが追いつかなかった", 4));

            CollectionAssert.Contains(session.Status.Value.Missing, "packetLog");
            var reason = session.TryBeginSubmit().Data.Missing.Single(missing => missing.Item == "packetLog").Reason;
            Assert.IsTrue(reason.Contains("書き込みが追いつかなかった"));
            Assert.IsTrue(reason.Contains("4"));
        }

        // 原点で記録すると、調査側はプレイヤーが本当に原点に居たと読んでしまう
        // Recording the origin makes an investigator read it as the player really having been there
        [Test]
        public void カメラとプレイヤーが無いことは実値ではなく欠損に載る()
        {
            var sources = new FakeBugReportCaptureSources { HasCamera = false, HasPlayer = false };
            var session = new BugReportCaptureSession(sources);

            LogAssert.Expect(LogType.Warning, new Regex("cameraState"));
            LogAssert.Expect(LogType.Warning, new Regex("playerState"));
            session.BeginOnPauseMenu();

            CollectionAssert.Contains(session.Status.Value.Missing, "cameraState");
            CollectionAssert.Contains(session.Status.Value.Missing, "playerState");
        }

        // 作業場を確保ごとに分けないと、次のEscapeが送信中の資料を消してしまう
        // Without a per-capture workspace the next Escape deletes materials that a send is still reading
        [Test]
        public void 確保ごとに別の作業場を使い退避とスクリーンショットをそこへ置く()
        {
            var sources = new FakeBugReportCaptureSources();
            var session = new BugReportCaptureSession(sources);
            session.BeginOnPauseMenu();
            session.OnServerCaptureCompleted(new ServerCaptureCompletion(7, 5, true, "/w/snapshots", "/master/server_v8", new List<string>(), new List<string>(), null, 0));
            var first = session.TryBeginSubmit().Data.CaptureWorkDirectory;
            Assert.AreEqual(first, sources.StagedWorkDirectory);
            Assert.AreEqual(first, sources.ScreenshotWorkDirectory);

            session.BeginOnPauseMenu();

            Assert.AreNotEqual(first, sources.ScreenshotWorkDirectory);
        }

        [Test]
        public void スクリーンショットに失敗すると欠損に載る()
        {
            var sources = new FakeBugReportCaptureSources { ScreenshotPath = null };
            var session = new BugReportCaptureSession(sources);

            LogAssert.Expect(LogType.Warning, new Regex("screenshot"));
            session.BeginOnPauseMenu();

            CollectionAssert.Contains(session.Status.Value.Missing, "screenshot");
        }

        [Test]
        public void 再開始で前回分は破棄される()
        {
            var sources = new FakeBugReportCaptureSources();
            var session = new BugReportCaptureSession(sources);
            session.BeginOnPauseMenu();
            session.OnServerCaptureCompleted(new ServerCaptureCompletion(7, 5, true, "/w/snapshots", "/master/server_v8", new List<string> { "tick_5.json" }, new List<string>(), null, 0));

            session.BeginOnPauseMenu();

            Assert.AreEqual(BugReportCaptureStatus.Capturing, session.Status.Value.Kind);
            Assert.AreEqual(2, sources.TakeRecordingCount);
            Assert.AreEqual(BugReportSubmitTicket.CapturePending, session.TryBeginSubmit().RefusedCode);
        }
    }
}
