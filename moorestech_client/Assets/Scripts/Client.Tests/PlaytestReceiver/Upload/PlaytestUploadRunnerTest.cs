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
            PlaytestOutboxTestBoxes.Make(_directories.ReportOutbox, "20260913_120000_aaaa", ("manifest.json", "{}"));
        }

        [TearDown]
        public void DeleteRoot()
        {
            PlaytestUploadRunner.ResetOnPlayMode();
            PlaytestLaunchProfile.ResetOnPlayMode();
            Directory.Delete(_root, true);
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
            PlaytestOutboxTestBoxes.Make(_directories.ReportOutbox, "20260913_150000_dddd", ("manifest.json", "{}"));
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
            Assert.AreEqual(1, ((PlaytestDeliveryLocalOnly)PlaytestDeliveryStatusBoard.Current.Value).PendingCount);
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
            Assert.AreEqual(1, ((PlaytestDeliveryRefused)PlaytestDeliveryStatusBoard.Current.Value).PendingCount);
        }

        [Test]
        public void 走行中に交換した開発者トークンを再走行で使う()
        {
            var gate = new UniTaskCompletionSource<PlaytestApiResult>();
            var api = new FakeUploadApi { PendingPut = gate };
            var runner = new PlaytestUploadRunner(api, _directories, new FakeTicketProvider("aabb"));
            runner.RequestUploadWith(new PlaytestDeveloperTokenRoute("first"));
            PlaytestOutboxTestBoxes.Make(_directories.ReportOutbox, "20260913_140000_cccc", ("manifest.json", "{}"));
            runner.RequestUploadWith(new PlaytestDeveloperTokenRoute("replacement"));
            gate.TrySetResult(PlaytestApiResult.Responded(200, "{}"));
            Assert.AreEqual(2, api.DeveloperSessionCallCount);
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
            var added = PlaytestOutboxTestBoxes.Make(_directories.ReportOutbox, "20260913_140000_cccc", ("manifest.json", "{}"));
            gate.TrySetResult(PlaytestApiResult.Responded(200, "{}"));
            Assert.AreEqual(1, api.DeveloperSessionCallCount);
            Assert.AreEqual(1, api.CompleteCount);
            Assert.IsFalse(File.Exists(Path.Combine(added, PlaytestOutboxScanner.UploadedMarker)));
            var status = (PlaytestDeliveryLocalOnly)PlaytestDeliveryStatusBoard.Current.Value;
            Assert.AreEqual(PlaytestLocalOnlyReason.DeveloperTokenMissing, status.Reason);
            Assert.AreEqual(1, status.PendingCount);
        }

        [Test]
        public void 走行中のローカル保存表示は走行終了でも維持して残数を更新する()
        {
            var gate = new UniTaskCompletionSource<PlaytestApiResult>();
            var api = new FakeUploadApi { PendingPut = gate };
            var runner = new PlaytestUploadRunner(api, _directories, new FakeTicketProvider("aabb"));
            runner.RequestUploadWith(new PlaytestDeveloperTokenRoute("first"));
            runner.RequestUploadWith(new PlaytestLocalOnlyRoute(PlaytestLocalOnlyReason.DeveloperTokenUnreadable, PlaytestOutboxScope.ReportsOnly, "unreadable"));
            Assert.AreEqual(1, ((PlaytestDeliveryLocalOnly)PlaytestDeliveryStatusBoard.Current.Value).PendingCount);
            gate.TrySetResult(PlaytestApiResult.Responded(200, "{}"));
            var status = (PlaytestDeliveryLocalOnly)PlaytestDeliveryStatusBoard.Current.Value;
            Assert.AreEqual(PlaytestLocalOnlyReason.DeveloperTokenUnreadable, status.Reason);
            Assert.AreEqual(0, status.PendingCount);
            Assert.AreEqual(1, api.CompleteCount);
        }

        [Test]
        public void 走行中の再要求は1本に保たれ終了直後にもう一度走る()
        {
            PlaytestOutboxTestBoxes.Make(_directories.ReportOutbox, "20260913_130000_bbbb", ("manifest.json", "{}"));
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
