using System.IO;
using Client.PlaytestReceiver.Delivery;
using Client.PlaytestReceiver.Credentials;
using Client.Game.InGame.BugReport.Playtest;
using Client.Game.InGame.BugReport.Submit;
using Client.PlaytestReceiver;
using Client.PlaytestReceiver.Launch;
using Client.PlaytestReceiver.Http;
using Client.PlaytestReceiver.Upload;
using Cysharp.Threading.Tasks;
using NUnit.Framework;

namespace Client.Tests.PlaytestReceiver
{
    public class PlaytestUploadRunnerTest
    {
        private string _root;
        private PlaytestOutboxDirectories _directories;

        [SetUp]
        public void CreateRoot()
        {
            PlaytestUploadRunner.ResetOnPlayMode();
            _root = Path.Combine(Path.GetTempPath(), "playtest-runner-" + Path.GetRandomFileName());
            _directories = PlaytestOutboxTestBoxes.Directories(_root);
            PlaytestOutboxTestBoxes.Make(_directories.ReportOutbox, "20260913_120000_aaaa", ("manifest.json", "{\"isEditor\":false}"));
        }

        [TearDown]
        public void DeleteRoot()
        {
            PlaytestUploadRunner.ResetOnPlayMode();
            PlaytestLaunchProfile.ResetOnPlayMode();
            Directory.Delete(_root, true);
        }
        [Test]
        public void 読めない箱と正常箱が共存しても正常箱を処理する()
        {
            if (System.Environment.OSVersion.Platform == System.PlatformID.Win32NT || System.Environment.UserName == "root")
                Assert.Ignore("chmodの権限拒否を再現できる非rootのUnixで検証する");
            var bad = PlaytestOutboxTestBoxes.Make(_directories.ReportOutbox, "bad", ("manifest.json", "{}"));
            using (var chmod = System.Diagnostics.Process.Start("/bin/chmod", "000 " + bad)) { chmod.WaitForExit(); Assert.AreEqual(0, chmod.ExitCode); }
            try
            {
                var api = new FakeUploadApi();
                new PlaytestUploadRunner(api, _directories, new FakeTicketProvider("aabb")).RequestUploadWith(new PlaytestDeveloperTokenRoute("token"));
                Assert.AreEqual(1, api.CompleteCount);
                Assert.IsInstanceOf<PlaytestOutboxInventoryUnavailable>(((PlaytestDeliveryPending)PlaytestDeliveryStatusBoard.Current.Value).Inventory);
            }
            finally { using var chmod = System.Diagnostics.Process.Start("/bin/chmod", "700 " + bad); chmod.WaitForExit(); }
        }

        [Test]
        public void 恒久失敗箱は完了とせずローカル保存時も失敗件数を残す()
        {
            var box = Directory.GetDirectories(_directories.ReportOutbox)[0];
            File.WriteAllText(Path.Combine(box, PlaytestOutboxScanner.FailedMarker), "failed");
            var runner = new PlaytestUploadRunner(new FakeUploadApi(), _directories, new FakeTicketProvider("aabb"));
            runner.RequestUploadWith(new PlaytestDeveloperTokenRoute("token"));
            var pending = (PlaytestDeliveryPending)PlaytestDeliveryStatusBoard.Current.Value;
            Assert.AreEqual(0, ((PlaytestOutboxDeliveryCounts)pending.Inventory).PendingCount);
            Assert.AreEqual(1, ((PlaytestOutboxDeliveryCounts)pending.Inventory).FailedCount);
            runner.RequestUploadWith(new PlaytestLocalOnlyRoute(PlaytestLocalOnlyReason.DeveloperTokenMissing, PlaytestOutboxScope.ReportsOnly, "removed"));
            Assert.AreEqual(1, ((PlaytestOutboxDeliveryCounts)((PlaytestDeliveryLocalOnly)PlaytestDeliveryStatusBoard.Current.Value).Inventory).FailedCount);
        }

        [Test]
        public void 別の走行役も期限内のトークンを再利用する()
        {
            var api = new FakeUploadApi();
            var first = new PlaytestUploadRunner(api, _directories, new FakeTicketProvider("aabb"));
            PlaytestLaunchProfile.Apply(PlaytestLaunchKind.Distribution, new LocalSteamSessionIdentity("76561198000000001"));

            first.RequestUploadWith(new PlaytestSteamRoute());
            Assert.AreEqual(1, api.SessionCallCount);

            // シーンで走行役を組み直しても同じ認証済みセッションを使う
            // A runner rebuilt for another scene uses the same authenticated session
            PlaytestOutboxTestBoxes.Make(_directories.ReportOutbox, "20260913_150000_dddd", ("manifest.json", "{\"isEditor\":false}"));
            var second = new PlaytestUploadRunner(api, _directories, new FakeTicketProvider("different"));
            second.RequestUploadWith(new PlaytestSteamRoute());
            Assert.AreEqual(2, api.CompleteCount);
            Assert.AreEqual(1, api.SessionCallCount);
        }

        [Test]
        public void 開発者トークン無しは報告をローカル保存する()
        {
            var api = new FakeUploadApi();
            var runner = new PlaytestUploadRunner(api, _directories, new FakeTicketProvider("aabb"));
            runner.RequestUploadWith(PlaytestDeliveryRouteResolver.Resolve(false, new PlaytestDeveloperTokenAbsent()));
            Assert.AreEqual(0, api.SessionCallCount + api.PutAttemptCount);
            Assert.AreEqual(1, ((PlaytestOutboxDeliveryCounts)((PlaytestDeliveryLocalOnly)PlaytestDeliveryStatusBoard.Current.Value).Inventory).PendingCount);
        }

        [Test]
        public void 開発者トークンは報告だけを送って進行記録を残す()
        {
            var api = new FakeUploadApi();
            var runner = new PlaytestUploadRunner(api, _directories, new FakeTicketProvider("aabb"));
            var progress = PlaytestOutboxTestBoxes.Make(_directories.ProgressOutbox, "20260913_130000_bbbb", ("manifest.json", "{}"));
            var file = Path.Combine(_root, "developer-token.txt");
            File.WriteAllText(file, "mdt_" + new string('a', 64));
            var route = PlaytestDeliveryRouteResolver.Resolve(false, PlaytestDeveloperTokenFile.Read(file));
            runner.RequestUploadWith(route);
            Assert.AreEqual(1, api.DeveloperSessionCallCount);
            Assert.AreEqual(1, api.CompleteCount);
            Assert.IsFalse(File.Exists(Path.Combine(progress, PlaytestOutboxScanner.UploadedMarker)));
            Assert.IsInstanceOf<PlaytestDeliveryAllSent>(PlaytestDeliveryStatusBoard.Current.Value);
        }

        [Test]
        public void 開発者トークンの拒否を状態へ出す()
        {
            var api = new FakeUploadApi { SessionResult = PlaytestApiResult.Responded(401, "{}") };
            var runner = new PlaytestUploadRunner(api, _directories, new FakeTicketProvider("aabb"));
            runner.RequestUploadWith(new PlaytestDeveloperTokenRoute("refused"));
            Assert.AreEqual(1, ((PlaytestOutboxDeliveryCounts)((PlaytestDeliveryRefused)PlaytestDeliveryStatusBoard.Current.Value).Inventory).PendingCount);
        }

        [Test]
        public void 走行中に交換した開発者トークンを再走行で使う()
        {
            var gate = new UniTaskCompletionSource<PlaytestApiResult>();
            var api = new FakeUploadApi { PendingPut = gate };
            var runner = new PlaytestUploadRunner(api, _directories, new FakeTicketProvider("aabb"));
            runner.RequestUploadWith(new PlaytestDeveloperTokenRoute("first"));
            PlaytestOutboxTestBoxes.Make(_directories.ReportOutbox, "20260913_140000_cccc", ("manifest.json", "{\"isEditor\":false}"));
            runner.RequestUploadWith(new PlaytestDeveloperTokenRoute("replacement"));
            api.SessionResult = PlaytestApiResult.Responded(200, PlaytestSessionBodies.Allowed("token-replacement", "2999-01-01T00:00:00.000Z"));
            gate.TrySetResult(PlaytestApiResult.Responded(200, "{}"));
            Assert.AreEqual(2, api.DeveloperSessionCallCount);
            CollectionAssert.AreEqual(new[] { "first", "replacement" }, api.DeveloperTokens);
            CollectionAssert.AreEqual(new[] { "tok", "token-replacement" }, api.PrepareBearerTokens);
            Assert.AreEqual(2, api.CompleteCount);
        }

        [Test]
        public void 走行中の別トークン再要求を最新のローカル保存要求が解除する()
        {
            var gate = new UniTaskCompletionSource<PlaytestApiResult>();
            var api = new FakeUploadApi { PendingPut = gate };
            var runner = new PlaytestUploadRunner(api, _directories, new FakeTicketProvider("aabb"));
            runner.RequestUploadWith(new PlaytestDeveloperTokenRoute("first"));
            runner.RequestUploadWith(new PlaytestDeveloperTokenRoute("replacement"));
            runner.RequestUploadWith(new PlaytestLocalOnlyRoute(PlaytestLocalOnlyReason.DeveloperTokenMissing, PlaytestOutboxScope.ReportsOnly, "removed"));
            var added = PlaytestOutboxTestBoxes.Make(_directories.ReportOutbox, "20260913_140000_cccc", ("manifest.json", "{\"isEditor\":false}"));
            gate.TrySetResult(PlaytestApiResult.Responded(200, "{}"));
            Assert.AreEqual(1, api.DeveloperSessionCallCount);
            Assert.AreEqual(1, api.CompleteCount);
            Assert.IsFalse(File.Exists(Path.Combine(added, PlaytestOutboxScanner.UploadedMarker)));
            var status = (PlaytestDeliveryLocalOnly)PlaytestDeliveryStatusBoard.Current.Value;
            Assert.AreEqual(PlaytestLocalOnlyReason.DeveloperTokenMissing, status.Reason);
            Assert.AreEqual(1, ((PlaytestOutboxDeliveryCounts)status.Inventory).PendingCount);
        }

        [Test]
        public void 走行中のローカル保存表示は走行終了でも維持して残数を更新する()
        {
            var gate = new UniTaskCompletionSource<PlaytestApiResult>();
            var api = new FakeUploadApi { PendingPut = gate };
            var runner = new PlaytestUploadRunner(api, _directories, new FakeTicketProvider("aabb"));
            runner.RequestUploadWith(new PlaytestDeveloperTokenRoute("first"));
            runner.RequestUploadWith(new PlaytestLocalOnlyRoute(PlaytestLocalOnlyReason.DeveloperTokenUnreadable, PlaytestOutboxScope.ReportsOnly, "unreadable"));
            Assert.AreEqual(1, ((PlaytestOutboxDeliveryCounts)((PlaytestDeliveryLocalOnly)PlaytestDeliveryStatusBoard.Current.Value).Inventory).PendingCount);
            gate.TrySetResult(PlaytestApiResult.Responded(200, "{}"));
            var status = (PlaytestDeliveryLocalOnly)PlaytestDeliveryStatusBoard.Current.Value;
            Assert.AreEqual(PlaytestLocalOnlyReason.DeveloperTokenUnreadable, status.Reason);
            Assert.AreEqual(0, ((PlaytestOutboxDeliveryCounts)status.Inventory).PendingCount);
            Assert.AreEqual(1, api.CompleteCount);
        }

        [Test]
        public void 走行中の再要求は1本に保たれ終了直後にもう一度走る()
        {
            PlaytestOutboxTestBoxes.Make(_directories.ReportOutbox, "20260913_130000_bbbb", ("manifest.json", "{\"isEditor\":false}"));
            var gate = new UniTaskCompletionSource<PlaytestApiResult>();
            var api = new FakeUploadApi { PendingPut = gate };
            var runner = new PlaytestUploadRunner(api, _directories, new FakeTicketProvider("aabb"));
            PlaytestLaunchProfile.Apply(PlaytestLaunchKind.Distribution, new LocalSteamSessionIdentity("76561198000000001"));

            runner.RequestUploadWith(new PlaytestSteamRoute());
            runner.RequestUploadWith(new PlaytestSteamRoute());

            // 1本目が最初のPUTで止まっている間は、2本目が走っていないので PUT は1回しか起きない
            // While the first run is parked on its first PUT, no second run exists, so exactly one PUT happened
            Assert.AreEqual(1, api.PutAttemptCount);

            // 箱固有の恒久失敗（必須ファイルが手元で消えた）で解放すると1本目は再試行せず両方の箱を1回ずつ試し、記録されていた再要求で2本目も同じく両方を試す
            // Releasing with a box-level permanent failure (a required file vanished locally) makes the first run try each box once without retrying, and the remembered re-request makes a second run do the same
            gate.TrySetResult(PlaytestApiResult.LocalFileChanged("manifest.json does not exist"));
            Assert.AreEqual(4, api.PutAttemptCount);
        }
    }
}
