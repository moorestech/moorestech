using System.IO;
using Client.PlaytestReceiver.Delivery;
using Client.PlaytestReceiver.Recovery;
using Client.PlaytestReceiver.Upload;
using Client.PlaytestReceiver.BoxFilePolicies;
using NUnit.Framework;

namespace Client.Tests.PlaytestReceiver.Recovery
{
    public class PlaytestFailedBoxReleaseTest
    {
        private string _root;
        private PlaytestOutboxDirectories _directories;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "release-" + Path.GetRandomFileName());
            _directories = new PlaytestOutboxDirectories(Path.Combine(_root, "reports"), new BugReportBoxFilePolicy(), Path.Combine(_root, "progress"), new ProgressRecordBoxFilePolicy());
        }

        [TearDown]
        public void TearDown() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }

        [Test]
        public void RemovesOnlyFailureAndAttemptMarkers()
        {
            var box = MakeBox("reports", "a", "READY", "UPLOAD_FAILED", "UPLOAD_ATTEMPTS", "UPLOAD_DECLARED", "UPLOAD_SKIPPED");
            Assert.AreEqual(1, PlaytestFailedBoxRelease.ReleaseAll(_directories, PlaytestOutboxScope.ReportsAndProgress));
            CollectionAssert.AreEquivalent(new[] { "READY", "UPLOAD_DECLARED", "UPLOAD_SKIPPED", "payload.txt" }, Names(box));
        }

        [Test]
        public void LeavesPendingAndDeliveredBoxesUntouched()
        {
            var pending = MakeBox("reports", "p", "READY");
            var delivered = MakeBox("reports", "d", "READY", "UPLOADED", "UPLOAD_FAILED");
            Assert.AreEqual(0, PlaytestFailedBoxRelease.ReleaseAll(_directories, PlaytestOutboxScope.ReportsAndProgress));
            CollectionAssert.AreEquivalent(new[] { "READY", "payload.txt" }, Names(pending));
            CollectionAssert.AreEquivalent(new[] { "READY", "UPLOADED", "UPLOAD_FAILED", "payload.txt" }, Names(delivered));
        }

        [Test]
        public void ReportsOnlyScopeKeepsProgressFailures()
        {
            MakeBox("reports", "r", "READY", "UPLOAD_FAILED");
            var progress = MakeBox("progress", "g", "READY", "UPLOAD_FAILED");
            Assert.AreEqual(1, PlaytestFailedBoxRelease.ReleaseAll(_directories, PlaytestOutboxScope.ReportsOnly));
            Assert.IsTrue(File.Exists(Path.Combine(progress, "UPLOAD_FAILED")));
        }

        [Test]
        public void MarkerDirectoryIsReportedAndKept()
        {
            var box = MakeBox("reports", "x", "READY");
            Directory.CreateDirectory(Path.Combine(box, "UPLOAD_FAILED"));
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Warning, new System.Text.RegularExpressions.Regex("failure marker is a directory"));
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Warning, new System.Text.RegularExpressions.Regex("cannot release"));
            Assert.AreEqual(0, PlaytestFailedBoxRelease.ReleaseAll(_directories, PlaytestOutboxScope.ReportsAndProgress));
            Assert.IsTrue(Directory.Exists(Path.Combine(box, "UPLOAD_FAILED")));
        }

        [Test]
        public void MissingOutboxReleasesNothing()
        {
            Assert.AreEqual(0, PlaytestFailedBoxRelease.ReleaseAll(_directories, PlaytestOutboxScope.ReportsAndProgress));
        }

        private sealed class CountingRequester : Client.Game.InGame.BugReport.Submit.IPlaytestUploadRequester
        {
            public int Count { get; private set; }
            public void RequestUpload() { Count++; }
        }

        [Test]
        public void ResendReleasesThenRequestsUploadOnce()
        {
            var box = MakeBox("reports", "a", "READY", "UPLOAD_FAILED");
            var requester = new CountingRequester();
            new PlaytestOutboxRecovery(requester, _directories, null, null).ResendFailedAsync(new PlaytestSteamRoute()).GetAwaiter().GetResult();
            Assert.IsFalse(File.Exists(Path.Combine(box, "UPLOAD_FAILED")));
            Assert.AreEqual(1, requester.Count);
        }

        [Test]
        public void CapacityHoldBoxKeepsItsMarkerAndIsNotCounted()
        {
            var box = MakeBox("reports", "cap", "READY");
            File.WriteAllText(Path.Combine(box, "UPLOAD_FAILED"), "repo/commits.bundle: too-large");
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Warning, new System.Text.RegularExpressions.Regex("resend keeps a hold"));
            Assert.AreEqual(0, PlaytestFailedBoxRelease.ReleaseAll(_directories, PlaytestOutboxScope.ReportsAndProgress));
            Assert.AreEqual("repo/commits.bundle: too-large", File.ReadAllText(Path.Combine(box, "UPLOAD_FAILED")));
        }

        [Test]
        public void CompletionHoldBoxKeepsItsMarker()
        {
            var box = MakeBox("reports", "comp", "READY");
            File.WriteAllText(Path.Combine(box, "UPLOAD_FAILED"), PlaytestOutboxScanner.CompletionHoldPrefix + "repo/commit-evidence: x");
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Warning, new System.Text.RegularExpressions.Regex("resend keeps a hold"));
            Assert.AreEqual(0, PlaytestFailedBoxRelease.ReleaseAll(_directories, PlaytestOutboxScope.ReportsAndProgress));
            Assert.IsTrue(File.Exists(Path.Combine(box, "UPLOAD_FAILED")));
        }

        [Test]
        public void UnreadableHoldBoxKeepsMarkerAndOtherBoxesStillResend()
        {
            var broken = MakeBox("reports", "a-broken", "READY");
            File.WriteAllText(Path.Combine(broken, "UPLOAD_FAILED"), "元repoまたはmanifestのSHAを特定できないため送信を保留しました");
            File.WriteAllText(Path.Combine(broken, "manifest.json"), "{ broken");
            var healthy = MakeBox("reports", "b-healthy", "READY", "UPLOAD_FAILED");
            var requester = new CountingRequester();
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Warning, new System.Text.RegularExpressions.Regex("cannot release"));
            new PlaytestOutboxRecovery(requester, _directories, null, null).ResendFailedAsync(new PlaytestSteamRoute()).GetAwaiter().GetResult();
            Assert.IsTrue(File.Exists(Path.Combine(broken, "UPLOAD_FAILED")));
            Assert.IsFalse(File.Exists(Path.Combine(healthy, "UPLOAD_FAILED")));
            Assert.AreEqual(1, requester.Count);
        }

        [Test]
        public void LocalOnlyRouteResendKeepsMarkersAndRequestsUploadOnce()
        {
            var box = MakeBox("reports", "a", "READY", "UPLOAD_FAILED");
            var requester = new CountingRequester();
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Warning, new System.Text.RegularExpressions.Regex("local-only"));
            var route = new PlaytestLocalOnlyRoute(PlaytestLocalOnlyReason.DistributionUnavailable, PlaytestOutboxScope.ReportsAndProgress, "x");
            new PlaytestOutboxRecovery(requester, _directories, null, null).ResendFailedAsync(route).GetAwaiter().GetResult();
            Assert.IsTrue(File.Exists(Path.Combine(box, "UPLOAD_FAILED")));
            Assert.AreEqual(1, requester.Count);
        }

        [Test]
        public void RefusedBoardResendKeepsMarkersAndRequestsUploadOnce()
        {
            var box = MakeBox("reports", "a", "READY", "UPLOAD_FAILED");
            var requester = new CountingRequester();
            PlaytestDeliveryStatusBoard.Set(new PlaytestDeliveryRefused(new PlaytestOutboxDeliveryCounts(0, 1)));
            try
            {
                UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Warning, new System.Text.RegularExpressions.Regex("refused the session"));
                new PlaytestOutboxRecovery(requester, _directories, null, null).ResendFailedAsync(new PlaytestSteamRoute()).GetAwaiter().GetResult();
                Assert.IsTrue(File.Exists(Path.Combine(box, "UPLOAD_FAILED")));
                Assert.AreEqual(1, requester.Count);
            }
            finally { PlaytestDeliveryStatusBoard.ResetOnPlayMode(); }
        }

        private string MakeBox(string outbox, string id, params string[] markers)
        {
            var box = Directory.CreateDirectory(Path.Combine(_root, outbox, id)).FullName;
            File.WriteAllText(Path.Combine(box, "payload.txt"), "x");
            foreach (var marker in markers) File.WriteAllText(Path.Combine(box, marker), "reason");
            return box;
        }

        private static string[] Names(string box)
        {
            return System.Array.ConvertAll(Directory.GetFiles(box), Path.GetFileName);
        }
    }
}
