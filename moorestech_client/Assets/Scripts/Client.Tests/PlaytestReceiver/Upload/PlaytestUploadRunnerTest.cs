using System.IO;
using Client.Game.InGame.BugReport.Submit;
using Client.PlaytestReceiver;
using Client.PlaytestReceiver.Gate;
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
            _root = Path.Combine(Path.GetTempPath(), "playtest-runner-" + Path.GetRandomFileName());
            _directories = new PlaytestOutboxDirectories(Path.Combine(_root, "BugReports", "outbox"), Path.Combine(_root, "ProgressRecords", "outbox"));
            Directory.CreateDirectory(_directories.ReportOutbox);
            Directory.CreateDirectory(_directories.ProgressOutbox);
            PlaytestOutboxTestBoxes.Make(_directories.ReportOutbox, "20260913_120000_aaaa", ("manifest.json", "{}"));
        }

        [TearDown]
        public void DeleteRoot()
        {
            PlaytestLaunchGate.SetCurrent(PlaytestGateResult.NotEvaluated);
            Directory.Delete(_root, true);
        }

        [Test]
        public void 照合を通っていれば送り開発者モードや不許可では何も送らない()
        {
            // 送るかどうかは押し場ではなく走行役が照合結果から決める。押し場を増やしても判定は増えない
            // The runner, not the push site, decides from the verdict; adding push sites never adds another decision
            var api = new FakeUploadApi();
            IPlaytestUploadRequester runner = new PlaytestUploadRunner(api, _directories);

            PlaytestLaunchGate.SetCurrent(PlaytestGateResult.DeveloperMode);
            runner.RequestUpload();
            PlaytestLaunchGate.SetCurrent(PlaytestGateResult.Blocked(PlaytestGateStatus.NotAllowed, ""));
            runner.RequestUpload();
            Assert.AreEqual(0, api.SessionCallCount + api.PutAttemptCount);

            PlaytestLaunchGate.SetCurrent(PlaytestGateResult.Allowed(new PlaytestSession(api, new FakeTicketProvider("aabb"))));
            runner.RequestUpload();
            Assert.AreEqual(1, api.PutAttemptCount);
            Assert.AreEqual(1, api.CompleteCount);
        }

        [Test]
        public void 走行中の再要求は1本に保たれ終了直後にもう一度走る()
        {
            PlaytestOutboxTestBoxes.Make(_directories.ReportOutbox, "20260913_130000_bbbb", ("manifest.json", "{}"));
            var gate = new UniTaskCompletionSource<PlaytestApiResult>();
            var api = new FakeUploadApi { PendingPut = gate };
            IPlaytestUploadRequester runner = new PlaytestUploadRunner(api, _directories);
            PlaytestLaunchGate.SetCurrent(PlaytestGateResult.Allowed(new PlaytestSession(api, new FakeTicketProvider("aabb"))));

            runner.RequestUpload();
            runner.RequestUpload();

            // 1本目が最初のPUTで止まっている間は、2本目が走っていないので PUT は1回しか起きない
            // While the first run is parked on its first PUT, no second run exists, so exactly one PUT happened
            Assert.AreEqual(1, api.PutAttemptCount);

            // 箱固有の恒久失敗（署名不一致の403）で解放すると1本目は再試行せず両方の箱を1回ずつ試し、記録されていた再要求で2本目も同じく両方を試す
            // Releasing with a box-level permanent failure (a signature-mismatch 403) makes the first run try each box once without retrying, and the remembered re-request makes a second run do the same
            gate.TrySetResult(PlaytestApiResult.Responded(403, "<Error><Code>SignatureDoesNotMatch</Code></Error>"));
            Assert.AreEqual(4, api.PutAttemptCount);
        }
    }
}
