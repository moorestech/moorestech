using System.IO;
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
            _root = Path.Combine(Path.GetTempPath(), "playtest-runner-" + Path.GetRandomFileName());
            _directories = PlaytestOutboxTestBoxes.Directories(_root);
            PlaytestOutboxTestBoxes.Make(_directories.ReportOutbox, "20260913_120000_aaaa", ("manifest.json", "{}"));
        }

        [TearDown]
        public void DeleteRoot()
        {
            PlaytestLaunchProfile.ResetForTest();
            Directory.Delete(_root, true);
        }

        [Test]
        public void 配布版は送信時に認証し開発者モードは送らない()
        {
            // 送るかどうかは押し場ではなく走行役が配布版判定から決める。押し場を増やしても判定は増えない
            // The runner, not the push site, decides from the launch kind; adding push sites never adds another decision
            var api = new FakeUploadApi();
            IPlaytestUploadRequester runner = new PlaytestUploadRunner(api, _directories, new FakeTicketProvider("aabb"));

            PlaytestLaunchProfile.SetForTest(PlaytestLaunchKind.DeveloperMode, "");
            runner.RequestUpload();
            Assert.AreEqual(0, api.SessionCallCount + api.PutAttemptCount);

            PlaytestLaunchProfile.SetForTest(PlaytestLaunchKind.Distribution, "76561198000000001");
            runner.RequestUpload();
            Assert.AreEqual(1, api.PutAttemptCount);
            Assert.AreEqual(1, api.CompleteCount);
            Assert.AreEqual(1, api.SessionCallCount);
            Assert.AreEqual(1, api.SessionCallsAtFirstPrepare);

            // 別の走行でも同じトークンを再利用する
            // A later run reuses the same token
            PlaytestOutboxTestBoxes.Make(_directories.ReportOutbox, "20260913_140000_cccc", ("manifest.json", "{}"));
            runner.RequestUpload();
            Assert.AreEqual(2, api.CompleteCount);
            Assert.AreEqual(1, api.SessionCallCount);
        }

        [Test]
        public void 走行中の再要求は1本に保たれ終了直後にもう一度走る()
        {
            PlaytestOutboxTestBoxes.Make(_directories.ReportOutbox, "20260913_130000_bbbb", ("manifest.json", "{}"));
            var gate = new UniTaskCompletionSource<PlaytestApiResult>();
            var api = new FakeUploadApi { PendingPut = gate };
            IPlaytestUploadRequester runner = new PlaytestUploadRunner(api, _directories, new FakeTicketProvider("aabb"));
            PlaytestLaunchProfile.SetForTest(PlaytestLaunchKind.Distribution, "76561198000000001");

            runner.RequestUpload();
            runner.RequestUpload();

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
