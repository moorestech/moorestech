using System.Collections.Generic;
using System.Text.RegularExpressions;
using Client.Game.InGame.BugReport;
using Client.Game.InGame.BugReport.Capture;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
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
            session.OnServerCaptureCompleted(7, 5, true, "/w/snapshots", "/master/server_v8", new List<string> { "tick_5.json" }, new List<string>());

            Assert.IsTrue(session.Status.Value.CapturePending, "退避中なのに確保が終わったことになっている");
            LogAssert.Expect(LogType.Warning, new Regex(BugReportSubmitTicket.CapturePending));
            Assert.AreEqual(BugReportSubmitTicket.CapturePending, session.TryBeginSubmit().RefusedCode);

            sources.ReleaseStaging();

            Assert.IsFalse(session.Status.Value.CapturePending);
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
            session.OnServerCaptureCompleted(7, 5, true, "/w/snapshots", "/master/server_v8", new List<string> { "tick_5.json" }, new List<string>());

            Assert.IsTrue(session.Status.Value.CapturePending, "スクリーンショット待ちなのに確保が終わったことになっている");
            LogAssert.Expect(LogType.Warning, new Regex(BugReportSubmitTicket.CapturePending));
            Assert.AreEqual(BugReportSubmitTicket.CapturePending, session.TryBeginSubmit().RefusedCode);

            screenshot.TrySetResult("/tmp/shot.png");

            Assert.IsFalse(session.Status.Value.CapturePending);
            Assert.IsTrue(session.TryBeginSubmit().Allowed);
        }

        // 同じ確保から2箱作ると同じ報告のdraft PRが2本出る
        // Two boxes from one capture raise two draft PRs for one report
        [Test]
        public void 書き出し中の再送と送信済みの再送を塞ぐ()
        {
            var sources = new FakeBugReportCaptureSources();
            var session = new BugReportCaptureSession(sources);
            session.BeginOnPauseMenu();
            session.OnServerCaptureCompleted(7, 5, true, "/w/snapshots", "/master/server_v8", new List<string> { "tick_5.json" }, new List<string>());

            var ticket = session.TryBeginSubmit();
            Assert.IsTrue(ticket.Allowed);
            LogAssert.Expect(LogType.Warning, new Regex(BugReportSubmitTicket.SubmitInFlight));
            Assert.AreEqual(BugReportSubmitTicket.SubmitInFlight, session.TryBeginSubmit().RefusedCode);

            session.CompleteSubmit(ticket.Data, true, new List<MissingItem>());

            LogAssert.Expect(LogType.Warning, new Regex(BugReportSubmitTicket.AlreadySubmitted));
            Assert.AreEqual(BugReportSubmitTicket.AlreadySubmitted, session.TryBeginSubmit().RefusedCode);
        }

        // 書き出せなかった箱は運搬されない。送信済みにすると残った資料で送り直す道が塞がる
        // An unwritten box is never shipped, so marking it sent would close the retry with whatever survived
        [Test]
        public void 書き出しに失敗した送信は送信済みにしない()
        {
            var sources = new FakeBugReportCaptureSources();
            var session = new BugReportCaptureSession(sources);
            session.BeginOnPauseMenu();
            session.OnServerCaptureCompleted(7, 5, true, "/w/snapshots", "/master/server_v8", new List<string> { "tick_5.json" }, new List<string>());

            var ticket = session.TryBeginSubmit();
            session.CompleteSubmit(ticket.Data, false, new List<MissingItem>());

            Assert.IsTrue(session.TryBeginSubmit().Allowed);
        }

        // 書き出し時に判明した欠損を戻さないと、報告者は欠けたまま送ったことを知る機会が無い
        // Without feeding back the missing items found while writing, the reporter never learns what was dropped
        [Test]
        public void 書き出しで判明した欠損が確保状態へ戻る()
        {
            var sources = new FakeBugReportCaptureSources();
            var session = new BugReportCaptureSession(sources);
            session.BeginOnPauseMenu();
            session.OnServerCaptureCompleted(7, 5, true, "/w/snapshots", "/master/server_v8", new List<string> { "tick_5.json" }, new List<string>());

            var ticket = session.TryBeginSubmit();
            session.CompleteSubmit(ticket.Data, true, new List<MissingItem> { new() { Item = "video", Reason = "ffmpegが見つからなかった" } });

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
            session.OnServerCaptureCompleted(7, 5, true, "/w/snapshots", "/master/server_v8", new List<string> { "tick_5.json" }, new List<string>());

            CollectionAssert.Contains(session.Status.Value.Missing, "tick_5.json");
            Assert.AreEqual(0, session.TryBeginSubmit().Data.SnapshotFileNames.Count);
        }
    }
}
