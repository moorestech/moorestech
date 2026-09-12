using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Client.Game.InGame.BugReport;
using Client.Game.InGame.BugReport.Capture;
using Client.Game.InGame.UI.UIState;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.BugReport
{
    public class BugReportCaptureSessionTest
    {
        // 確保セッションの外部依存を全部握るフェイク。待ちはテストが任意の時点で解く
        // Fake holding every external dependency; the test releases the wait whenever it wants
        private sealed class FakeSources : IBugReportCaptureSources
        {
            public BugReportServerCaptureRequest RequestResult = new(true, 7, null);
            public int CutCount;
            public string Unavailable = "";
            public string ScreenshotPath = "/tmp/shot.png";
            public UIStateEnum CurrentUiState = UIStateEnum.PauseMenu;

            private readonly UniTaskCompletionSource _timeout = new();

            public void ElapseServerCaptureTimeout()
            {
                _timeout.TrySetResult();
            }

            public UniTask<BugReportServerCaptureRequest> RequestServerCapture() => UniTask.FromResult(RequestResult);
            public UniTask WaitServerCaptureTimeout() => _timeout.Task;
            public void CutRecordingSegment() => CutCount++;
            public IReadOnlyList<string> CompletedVideoSegments() => new List<string> { "/tmp/seg_00.mp4" };
            public string RecordingUnavailableReason() => Unavailable;
            public IReadOnlyList<(long unixMs, ulong tick)> FrameTicks() => new List<(long, ulong)> { (1, 2) };
            public IReadOnlyList<UnityLogEntry> Logs() => new List<UnityLogEntry>();
            public void SetCurrentUiState(UIStateEnum uiState) => CurrentUiState = uiState;
            public ClientStateSnapshot ClientState() => new(Vector3.zero, Vector3.zero, Vector3.zero, CurrentUiState.ToString(), 2);
            public UniTask<string> CaptureScreenshot() => UniTask.FromResult(ScreenshotPath);
        }

        [Test]
        public void 開始で確保中になり完了イベントで解除される()
        {
            var sources = new FakeSources();
            var session = new BugReportCaptureSession(sources);
            session.BeginOnPauseMenu();
            Assert.IsTrue(session.Status.Value.HasSession);
            Assert.IsTrue(session.Status.Value.CapturePending);
            Assert.AreEqual(1, sources.CutCount);

            session.OnServerCaptureCompleted(7, 5, true, "/w/snapshots", "/master/server_v8", new List<string> { "tick_5.json" }, new List<string> { "packets_6.bin" });

            Assert.IsFalse(session.Status.Value.CapturePending);
            var data = session.TakeCapturedData();
            Assert.AreEqual(5UL, data.ReportTick);
            Assert.AreEqual("/w/snapshots", data.SnapshotDirectory);

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
            var sources = new FakeSources();
            var session = new BugReportCaptureSession(sources);
            session.BeginOnPauseMenu();

            session.OnServerCaptureCompleted(99, 5, true, "/w/snapshots", "/master/server_v8", new List<string>(), new List<string>());

            Assert.IsTrue(session.Status.Value.CapturePending);
            Assert.IsNull(session.TakeCapturedData().SnapshotDirectory);
        }

        [Test]
        public void サーバーが要求を拒否したら待たずに欠損へ載せる()
        {
            var sources = new FakeSources { RequestResult = new BugReportServerCaptureRequest(false, 0, "常時記録が無効") };
            var session = new BugReportCaptureSession(sources);

            LogAssert.Expect(LogType.Warning, new Regex("serverSnapshot"));
            session.BeginOnPauseMenu();

            Assert.IsFalse(session.Status.Value.CapturePending);
            CollectionAssert.Contains(session.Status.Value.Missing, "serverSnapshot");
            Assert.IsTrue(session.TakeCapturedData().Missing.Single(missing => missing.Item == "serverSnapshot").Reason.Contains("常時記録が無効"));
        }

        [Test]
        public void 拒否されたあとに来た完了イベントは取り込まない()
        {
            var sources = new FakeSources { RequestResult = new BugReportServerCaptureRequest(false, 0, "常時記録が無効") };
            var session = new BugReportCaptureSession(sources);
            LogAssert.Expect(LogType.Warning, new Regex("serverSnapshot"));
            session.BeginOnPauseMenu();

            session.OnServerCaptureCompleted(0, 9, true, "/w/snapshots", "/master/server_v8", new List<string> { "tick_9.json" }, new List<string>());

            Assert.IsNull(session.TakeCapturedData().SnapshotDirectory);
        }

        [Test]
        public void サーバーの書き出し失敗は欠損へ載せて待ちを打ち切る()
        {
            var sources = new FakeSources();
            var session = new BugReportCaptureSession(sources);
            session.BeginOnPauseMenu();

            LogAssert.Expect(LogType.Warning, new Regex("serverSnapshot"));
            session.OnServerCaptureCompleted(7, 5, false, "/w/snapshots", "/master/server_v8", new List<string> { "tick_5.json" }, new List<string>());

            Assert.IsFalse(session.Status.Value.CapturePending);
            CollectionAssert.Contains(session.Status.Value.Missing, "serverSnapshot");
            Assert.IsNull(session.TakeCapturedData().SnapshotDirectory);
        }

        [Test]
        public void 完了イベントが来ないままタイムアウトすると待ちを打ち切る()
        {
            var sources = new FakeSources();
            var session = new BugReportCaptureSession(sources);
            session.BeginOnPauseMenu();
            Assert.IsTrue(session.Status.Value.CapturePending);

            LogAssert.Expect(LogType.Warning, new Regex("serverSnapshot"));
            sources.ElapseServerCaptureTimeout();

            Assert.IsFalse(session.Status.Value.CapturePending);
            CollectionAssert.Contains(session.Status.Value.Missing, "serverSnapshot");
        }

        [Test]
        public void タイムアウトが来ても取り込み済みなら欠損にしない()
        {
            var sources = new FakeSources();
            var session = new BugReportCaptureSession(sources);
            session.BeginOnPauseMenu();
            session.OnServerCaptureCompleted(7, 5, true, "/w/snapshots", "/master/server_v8", new List<string>(), new List<string>());

            sources.ElapseServerCaptureTimeout();

            Assert.AreEqual(0, session.TakeCapturedData().Missing.Count);
            Assert.AreEqual("/w/snapshots", session.TakeCapturedData().SnapshotDirectory);
        }

        [Test]
        public void 録画が無効なら欠損に載る()
        {
            var sources = new FakeSources { Unavailable = "ffmpeg なし" };
            var session = new BugReportCaptureSession(sources);

            LogAssert.Expect(LogType.Warning, new Regex("video"));
            session.BeginOnPauseMenu();

            CollectionAssert.Contains(session.Status.Value.Missing, "video");
            Assert.AreEqual(0, sources.CutCount);
        }

        [Test]
        public void スクリーンショットに失敗すると欠損に載る()
        {
            var sources = new FakeSources { ScreenshotPath = null };
            var session = new BugReportCaptureSession(sources);

            LogAssert.Expect(LogType.Warning, new Regex("screenshot"));
            session.BeginOnPauseMenu();

            CollectionAssert.Contains(session.Status.Value.Missing, "screenshot");
        }

        [Test]
        public void 再開始で前回分は破棄される()
        {
            var sources = new FakeSources();
            var session = new BugReportCaptureSession(sources);
            session.BeginOnPauseMenu();
            session.OnServerCaptureCompleted(7, 5, true, "/w/snapshots", "/master/server_v8", new List<string> { "tick_5.json" }, new List<string>());

            session.BeginOnPauseMenu();

            Assert.IsTrue(session.Status.Value.CapturePending);
            Assert.AreEqual(2, sources.CutCount);
            Assert.IsNull(session.TakeCapturedData().SnapshotDirectory);
            Assert.AreEqual(0, session.TakeCapturedData().SnapshotFileNames.Count);
        }
    }
}
