using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using Client.PlaytestReceiver;
using Client.PlaytestReceiver.Http;
using Client.PlaytestReceiver.Steam;
using Client.PlaytestReceiver.Upload;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.PlaytestReceiver
{
    public class PlaytestUploaderTest
    {
        private string _root;
        private string _reports;
        private string _progress;

        [SetUp]
        public void CreateRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "playtest-upload-" + Path.GetRandomFileName());
            _reports = Path.Combine(_root, "BugReports", "outbox");
            _progress = Path.Combine(_root, "ProgressRecords", "outbox");
            Directory.CreateDirectory(_reports);
            Directory.CreateDirectory(_progress);
        }

        [TearDown]
        public void DeleteRoot()
        {
            Directory.Delete(_root, true);
        }

        [Test]
        public void READYの箱が送られUPLOADEDが付く()
        {
            var box = MakeBox(_reports, "20260913_120000_aaaa", ("manifest.json", "{\"kind\":\"bug\"}"));
            var api = new FakeApi();
            var sent = Upload(api);

            Assert.AreEqual(1, sent);
            Assert.AreEqual(new[] { "manifest.json" }, api.PutPaths.ToArray());
            Assert.AreEqual(1, api.CompleteCount);
            Assert.IsTrue(File.Exists(Path.Combine(box, PlaytestOutboxScanner.UploadedMarker)));
        }

        [Test]
        public void UPLOADED済みは再送されない()
        {
            MakeBox(_reports, "20260913_120000_aaaa", ("manifest.json", "{}"));
            var api = new FakeApi();
            Upload(api);
            Upload(api);

            Assert.AreEqual(1, api.CompleteCount);
        }

        [Test]
        public void 失敗すると試行回数が増え箱は残る()
        {
            var box = MakeBox(_reports, "20260913_120000_aaaa", ("manifest.json", "{}"));
            var api = new FakeApi { PutResult = new PlaytestApiResult { TransportError = "offline" } };

            Assert.AreEqual(0, Upload(api));
            Assert.IsFalse(File.Exists(Path.Combine(box, PlaytestOutboxScanner.UploadedMarker)));
            Assert.AreEqual("1", File.ReadAllText(Path.Combine(box, PlaytestOutboxScanner.AttemptsMarker)).Split('\n')[0]);
        }

        [Test]
        public void 失敗が5回に達した箱はUPLOAD_FAILEDになり後続の箱を塞がない()
        {
            var stuck = MakeBox(_reports, "20260913_110000_aaaa", ("manifest.json", "{}"));
            var api = new FakeApi { PutResult = new PlaytestApiResult { TransportError = "offline" } };
            LogAssert.Expect(LogType.Error, new Regex(".*giving up on 20260913_110000_aaaa.*"));
            for (var attempt = 0; attempt < PlaytestUploadAttemptLog.MaxAttempts; attempt++) Upload(api);

            Assert.IsTrue(File.Exists(Path.Combine(stuck, PlaytestOutboxScanner.FailedMarker)));

            var later = MakeBox(_reports, "20260913_120000_bbbb", ("manifest.json", "{}"));
            api.PutResult = new PlaytestApiResult { StatusCode = 200, Body = "{}" };

            Assert.AreEqual(1, Upload(api));
            Assert.IsTrue(File.Exists(Path.Combine(later, PlaytestOutboxScanner.UploadedMarker)));
        }

        [Test]
        public void 巨大ファイルは送らずskippedに載せて箱自体は完了する()
        {
            var box = MakeBox(_reports, "20260913_120000_aaaa", ("manifest.json", "{}"));
            var huge = Path.Combine(box, "video.mp4");
            using (var stream = new FileStream(huge, FileMode.Create))
            {
                stream.SetLength(PlaytestReceiverConfig.MaxFileBytes + 1);
            }
            var api = new FakeApi();

            Assert.AreEqual(1, Upload(api));
            Assert.AreEqual(new[] { "manifest.json" }, api.PutPaths.ToArray());
            StringAssert.Contains("video.mp4", api.LastSummary);
            StringAssert.Contains("skipped", api.LastSummary);
            Assert.IsTrue(File.Exists(Path.Combine(box, PlaytestOutboxScanner.UploadedMarker)));
        }

        [Test]
        public void トークンが取れなければ何も送らず箱を残す()
        {
            var box = MakeBox(_reports, "20260913_120000_aaaa", ("manifest.json", "{}"));
            var api = new FakeApi { SessionResult = new PlaytestApiResult { TransportError = "offline" } };

            Assert.AreEqual(0, Upload(api));
            Assert.IsEmpty(api.PutPaths);
            Assert.IsFalse(File.Exists(Path.Combine(box, PlaytestOutboxScanner.UploadedMarker)));
        }

        // 恒久的な4xxは何度送っても直らない。そのファイルだけ見送り、箱は完了させて後続を塞がない
        // A permanent 4xx never heals on retry, so only that file is dropped and the box still completes
        [Test]
        public void 恒久的な4xxのファイルは見送られ箱は完了する()
        {
            var box = MakeBox(_reports, "20260913_120000_aaaa", ("manifest.json", "{}"));
            var api = new FakeApi { PutResult = new PlaytestApiResult { StatusCode = 400, Body = "bad-path" } };

            Assert.AreEqual(1, Upload(api));
            Assert.AreEqual(1, api.CompleteCount);
            StringAssert.Contains("manifest.json", api.LastSummary);
            StringAssert.Contains("http-400", api.LastSummary);
            Assert.IsTrue(File.Exists(Path.Combine(box, PlaytestOutboxScanner.UploadedMarker)));
        }

        // 401はトークンの期限切れ。取り直して1回だけやり直し、失敗として数えない
        // A 401 means the token expired; it is refreshed and retried exactly once instead of counting as a failure
        [Test]
        public void 期限切れの401はトークンを取り直して1回だけやり直す()
        {
            var box = MakeBox(_reports, "20260913_120000_aaaa", ("manifest.json", "{}"));
            var api = new FakeApi();
            api.PutResultQueue.Enqueue(new PlaytestApiResult { StatusCode = 401, Body = "expired" });
            api.PutResultQueue.Enqueue(new PlaytestApiResult { StatusCode = 200, Body = "{}" });

            Assert.AreEqual(1, Upload(api));
            Assert.AreEqual(2, api.PutAttemptCount);
            Assert.AreEqual(2, api.SessionCallCount);
            Assert.IsFalse(File.Exists(Path.Combine(box, PlaytestOutboxScanner.AttemptsMarker)));
            Assert.IsTrue(File.Exists(Path.Combine(box, PlaytestOutboxScanner.UploadedMarker)));
        }

        // 受け口はR2のキーとしてUTF-8をそのまま受ける。クライアント側だけが厳しいと実ファイルが恒久的に失われる
        // The receiver takes UTF-8 verbatim as an R2 key; a stricter client rule would lose real files for good
        [Test]
        public void 日本語や空白を含む名前も見送らずに送る()
        {
            var box = MakeBox(_reports, "20260913_120000_aaaa", ("ユニティ.log", "x"), ("a b.log", "y"));
            var api = new FakeApi();

            Assert.AreEqual(1, Upload(api));
            CollectionAssert.AreEquivalent(new[] { "ユニティ.log", "a b.log" }, api.PutPaths);
            StringAssert.Contains("\"skipped\":[]", api.LastSummary);
            Assert.IsTrue(File.Exists(Path.Combine(box, PlaytestOutboxScanner.UploadedMarker)));
        }

        [Test]
        public void 走行中に再要求しても走行は1本に保たれる()
        {
            MakeBox(_reports, "20260913_120000_aaaa", ("manifest.json", "{}"));
            MakeBox(_reports, "20260913_130000_bbbb", ("manifest.json", "{}"));
            var gate = new UniTaskCompletionSource<PlaytestApiResult>();
            var api = new FakeApi { PendingPut = gate };
            IPlaytestUploadRequester runner = new PlaytestUploadRunner(api, _reports, _progress);
            var session = new PlaytestSession(api, new AlwaysTicketProvider());

            runner.RequestUpload(session);
            runner.RequestUpload(session);

            // 1本目が最初のPUTで止まっている間は、2本目が走っていないので PUT は1回しか起きない
            // While the first run is parked on its first PUT, no second run exists, so exactly one PUT happened
            Assert.AreEqual(1, api.PutAttemptCount);
            gate.TrySetResult(new PlaytestApiResult { TransportError = "offline" });
        }

        private int Upload(FakeApi api)
        {
            var session = new PlaytestSession(api, new AlwaysTicketProvider());
            var uploader = new PlaytestUploader(api, session, _reports, _progress);
            return uploader.UploadPendingAsync(DateTime.UtcNow, CancellationToken.None).GetAwaiter().GetResult();
        }

        private static string MakeBox(string outbox, string bundleId, params (string Name, string Content)[] files)
        {
            var box = Path.Combine(outbox, bundleId);
            Directory.CreateDirectory(box);
            foreach (var file in files) File.WriteAllText(Path.Combine(box, file.Name), file.Content);
            File.WriteAllText(Path.Combine(box, PlaytestOutboxScanner.ReadyMarker), "");
            return box;
        }

        private sealed class AlwaysTicketProvider : IPlaytestSteamTicketProvider
        {
            public bool IsSteamRunning() { return true; }
            public UniTask<string> RequestWebApiTicketHexAsync(CancellationToken token) { return UniTask.FromResult("aabb"); }
        }

        private sealed class FakeApi : IPlaytestReceiverApi
        {
            public readonly List<string> PutPaths = new();
            public readonly Queue<PlaytestApiResult> PutResultQueue = new();
            public int CompleteCount;
            public int PutAttemptCount;
            public int SessionCallCount;
            public string LastSummary = "";
            public UniTaskCompletionSource<PlaytestApiResult> PendingPut;
            public PlaytestApiResult PutResult = new() { StatusCode = 200, Body = "{}" };
            public PlaytestApiResult SessionResult = new() { StatusCode = 200, Body = "{\"steamId\":\"7656\",\"allowed\":true,\"token\":\"tok\"}" };

            public UniTask<PlaytestApiResult> PostSessionAsync(string ticketHex, CancellationToken token)
            {
                SessionCallCount++;
                return UniTask.FromResult(SessionResult);
            }

            public UniTask<PlaytestApiResult> PutFileAsync(string bearerToken, string kind, string bundleId, string relativePath, string absoluteFilePath, CancellationToken token)
            {
                PutAttemptCount++;
                if (PendingPut != null) return PendingPut.Task;

                var result = PutResultQueue.Count != 0 ? PutResultQueue.Dequeue() : PutResult;
                if (result.IsSuccess) PutPaths.Add(relativePath);
                return UniTask.FromResult(result);
            }

            public UniTask<PlaytestApiResult> PostCompleteAsync(string bearerToken, string kind, string bundleId, string summaryJson, CancellationToken token)
            {
                CompleteCount++;
                LastSummary = summaryJson;
                return UniTask.FromResult(new PlaytestApiResult { StatusCode = 200, Body = "{}" });
            }
        }
    }
}
