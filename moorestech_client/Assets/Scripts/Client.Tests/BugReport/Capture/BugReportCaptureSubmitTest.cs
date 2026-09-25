using System.Collections.Generic;
using System.Text.RegularExpressions;
using Client.Game.InGame.BugReport;
using Client.Game.InGame.BugReport.Capture;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UniRx;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.BugReport.Capture
{
    // 送信の門（確保中・二重送信・書き出し結果の戻し）の回帰ガード
    // Regression guard for the send gate: capture pending, double sends and feeding the write result back
    public class BugReportCaptureSubmitTest
    {
        // 退避が終わる前に送れると、直後に届く記録を捨てて世界データの無い箱が運搬される
        // Sending before staging finishes discards the records that arrive moments later and ships a box with no world data
        [Test]
        public void 退避が終わるまで送信を受け付けない()
        {
            var sources = new FakeBugReportCaptureSources();
            sources.HoldStaging();
            var session = new BugReportCaptureSession(sources);
            session.BeginOnPauseMenu();
            session.OnServerCaptureCompleted(new ServerCaptureCompletion(7, 5, true, "/w/snapshots", "/master/server_v8", new List<string> { "tick_5.json" }, new List<string>(), null, 0));

            Assert.AreEqual(BugReportCaptureStatus.Capturing, session.Status.Value.Kind, "退避中なのに確保が終わったことになっている");
            LogAssert.Expect(LogType.Warning, new Regex(BugReportSubmitTicket.CapturePending));
            Assert.AreEqual(BugReportSubmitTicket.CapturePending, session.TryBeginSubmit().RefusedCode);

            sources.ReleaseStaging();

            Assert.AreEqual(BugReportCaptureStatus.Ready, session.Status.Value.Kind);
            Assert.IsTrue(session.TryBeginSubmit().Allowed);
        }

        // 排出を待たずに送れると、Escape時点の最後の区間が揃う前に箱が閉じる
        // Sending before the drain finishes closes the box without the Escape moment's last segment
        [Test]
        public void 録画の排出が終わるまで送信を受け付けない()
        {
            var sources = new FakeBugReportCaptureSources();
            sources.HoldRecording();
            var session = new BugReportCaptureSession(sources);
            session.BeginOnPauseMenu();
            session.OnServerCaptureCompleted(new ServerCaptureCompletion(7, 5, true, "/w/snapshots", "/master/server_v8", new List<string> { "tick_5.json" }, new List<string>(), null, 0));

            Assert.AreEqual(BugReportCaptureStatus.Capturing, session.Status.Value.Kind, "録画の排出待ちなのに確保が終わったことになっている");
            LogAssert.Expect(LogType.Warning, new Regex(BugReportSubmitTicket.CapturePending));
            Assert.AreEqual(BugReportSubmitTicket.CapturePending, session.TryBeginSubmit().RefusedCode);

            sources.ReleaseRecording();

            Assert.AreEqual(BugReportCaptureStatus.Ready, session.Status.Value.Kind);
            Assert.IsTrue(session.TryBeginSubmit().Allowed);
        }

        [Test]
        public void スクリーンショットが終わるまで送信を受け付けない()
        {
            var sources = new FakeBugReportCaptureSources();
            var screenshot = new UniTaskCompletionSource<string>();
            sources.ScreenshotTask = screenshot;
            var session = new BugReportCaptureSession(sources);
            session.BeginOnPauseMenu();
            session.OnServerCaptureCompleted(new ServerCaptureCompletion(7, 5, true, "/w/snapshots", "/master/server_v8", new List<string> { "tick_5.json" }, new List<string>(), null, 0));

            Assert.AreEqual(BugReportCaptureStatus.Capturing, session.Status.Value.Kind, "スクリーンショット待ちなのに確保が終わったことになっている");
            LogAssert.Expect(LogType.Warning, new Regex(BugReportSubmitTicket.CapturePending));
            Assert.AreEqual(BugReportSubmitTicket.CapturePending, session.TryBeginSubmit().RefusedCode);

            screenshot.TrySetResult("/tmp/shot.png");

            Assert.AreEqual(BugReportCaptureStatus.Ready, session.Status.Value.Kind);
            Assert.IsTrue(session.TryBeginSubmit().Allowed);
        }

        // 送信中の重複を拒否し、成功後だけ新しい記録で次の送信を受け付ける
        // Reject duplicates in flight, then accept the next send only with fresh records after success
        [Test]
        public void 書き出し中は再送を塞ぎ成功後は再確保して次の送信を受け付ける()
        {
            var sources = new FakeBugReportCaptureSources();
            var session = new BugReportCaptureSession(sources);
            session.BeginOnPauseMenu();
            session.OnServerCaptureCompleted(new ServerCaptureCompletion(7, 5, true, "/w/snapshots", "/master/server_v8", new List<string> { "tick_5.json" }, new List<string>(), null, 0));

            var ticket = session.TryBeginSubmit();
            Assert.IsTrue(ticket.Allowed);
            LogAssert.Expect(LogType.Warning, new Regex(BugReportSubmitTicket.SubmitInFlight));
            Assert.AreEqual(BugReportSubmitTicket.SubmitInFlight, session.TryBeginSubmit().RefusedCode);

            var kinds = new List<string>();
            using var subscription = session.Status.Skip(1).Subscribe(status => kinds.Add(status.Kind));
            sources.RequestResult = new BugReportServerCaptureRequest(true, 8, null);
            sources.ScreenshotPath = "/tmp/second-shot.png";
            session.CompleteSubmit(ticket.Data, true, new List<MissingItem>());

            Assert.AreEqual(2, sources.TakeRecordingCount);
            Assert.AreEqual(BugReportCaptureStatus.Capturing, session.Status.Value.Kind);
            LogAssert.Expect(LogType.Warning, new Regex(BugReportSubmitTicket.CapturePending));
            Assert.AreEqual(BugReportSubmitTicket.CapturePending, session.TryBeginSubmit().RefusedCode);
            session.OnServerCaptureCompleted(new ServerCaptureCompletion(8, 9, true, "/w/snapshots", "/master/server_v8", new List<string>(), new List<string>(), null, 0));

            // 配信は既存の確保中→準備完了を使い、2件目の資料が最初と別であることを確かめる
            // Reuse the capturing-to-ready delivery and verify that the second report owns distinct materials
            Assert.AreEqual(BugReportCaptureStatus.Capturing, kinds[0]);
            Assert.AreEqual(BugReportCaptureStatus.Ready, kinds[kinds.Count - 1]);
            var second = session.TryBeginSubmit();
            Assert.IsTrue(second.Allowed);
            Assert.AreNotSame(ticket.Data, second.Data);
            Assert.AreNotEqual(ticket.Data.CaptureWorkDirectory, second.Data.CaptureWorkDirectory);
            Assert.AreEqual(9UL, second.Data.ReportTick);
            Assert.AreEqual("/tmp/second-shot.png", second.Data.ScreenshotPath);
        }

        // 書き出せなかった箱は運搬されない。送信済みにすると残った資料で送り直す道が塞がる
        // An unwritten box is never shipped, so marking it sent would close the retry with whatever survived
        [Test]
        public void 書き出しに失敗した送信は送信済みにしない()
        {
            var sources = new FakeBugReportCaptureSources();
            var session = new BugReportCaptureSession(sources);
            session.BeginOnPauseMenu();
            session.OnServerCaptureCompleted(new ServerCaptureCompletion(7, 5, true, "/w/snapshots", "/master/server_v8", new List<string> { "tick_5.json" }, new List<string>(), null, 0));

            var ticket = session.TryBeginSubmit();
            session.CompleteSubmit(ticket.Data, false, new List<MissingItem>());

            var retry = session.TryBeginSubmit();
            Assert.IsTrue(retry.Allowed);
            Assert.AreSame(ticket.Data, retry.Data);
            Assert.AreEqual(1, sources.TakeRecordingCount);
        }

        // 書き出し時に判明した欠損を戻さないと、報告者は欠けたまま送ったことを知る機会が無い
        // Without feeding back the missing items found while writing, the reporter never learns what was dropped
        [Test]
        public void 書き出し失敗で判明した欠損が確保状態へ戻る()
        {
            var sources = new FakeBugReportCaptureSources();
            var session = new BugReportCaptureSession(sources);
            session.BeginOnPauseMenu();
            session.OnServerCaptureCompleted(new ServerCaptureCompletion(7, 5, true, "/w/snapshots", "/master/server_v8", new List<string> { "tick_5.json" }, new List<string>(), null, 0));

            var ticket = session.TryBeginSubmit();
            session.CompleteSubmit(ticket.Data, false, new List<MissingItem> { new() { Item = "video", Reason = "ffmpegが見つからなかった" } });

            CollectionAssert.AreEqual(new[] { "video" }, session.Status.Value.Missing);
        }

        // 退避側が数えた欠損も確保状態に出す。出さないと「サーバー記録は揃っている」と見える
        // The staging side's missing items also surface in the capture state; otherwise the server records look complete
        [Test]
        public void 退避できなかったファイルは確保状態の欠損に出る()
        {
            var sources = new FakeBugReportCaptureSources
            {
                StagedSnapshots = new List<string>(),
                StagedPacketLogs = new List<string>(),
                StagingMissing = new List<MissingItem> { new() { Item = "tick_5.json", Reason = "確保しようとした時点で無かった" } },
            };
            var session = new BugReportCaptureSession(sources);
            session.BeginOnPauseMenu();

            LogAssert.Expect(LogType.Warning, new Regex("tick_5.json"));
            session.OnServerCaptureCompleted(new ServerCaptureCompletion(7, 5, true, "/w/snapshots", "/master/server_v8", new List<string> { "tick_5.json" }, new List<string>(), null, 0));

            CollectionAssert.Contains(session.Status.Value.Missing, "tick_5.json");
            Assert.AreEqual(0, session.TryBeginSubmit().Data.SnapshotFileNames.Count);
        }
    }
}
