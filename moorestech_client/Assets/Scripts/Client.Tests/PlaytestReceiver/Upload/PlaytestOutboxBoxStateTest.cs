using System.IO;
using Client.PlaytestReceiver.Upload;
using NUnit.Framework;

namespace Client.Tests.PlaytestReceiver.Upload
{
    public class PlaytestOutboxBoxStateTest
    {
        private string _root;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "box-state-" + Path.GetRandomFileName());
            Directory.CreateDirectory(_root);
        }

        [TearDown]
        public void TearDown() { Directory.Delete(_root, true); }

        [TestCase(new string[0], PlaytestOutboxBoxKind.NotReady)]
        [TestCase(new[] { "READY" }, PlaytestOutboxBoxKind.Pending)]
        [TestCase(new[] { "READY", "UPLOAD_FAILED" }, PlaytestOutboxBoxKind.Failed)]
        [TestCase(new[] { "READY", "UPLOADED" }, PlaytestOutboxBoxKind.Delivered)]
        [TestCase(new[] { "READY", "SHIPPED" }, PlaytestOutboxBoxKind.Delivered)]
        [TestCase(new[] { "READY", "UPLOADED", "UPLOAD_FAILED" }, PlaytestOutboxBoxKind.Delivered)]
        public void ClassifiesByRootMarkers(string[] markers, PlaytestOutboxBoxKind expected)
        {
            var box = Directory.CreateDirectory(Path.Combine(_root, "box")).FullName;
            foreach (var marker in markers) File.WriteAllText(Path.Combine(box, marker), "");
            Assert.AreEqual(expected, PlaytestOutboxBoxState.Classify(box));
        }

        [Test]
        public void FailureMarkerDirectoryCountsAsFailed()
        {
            // 印を書けない同名ディレクトリも失敗として数える（既存の件数表示と同じ判定）
            // A directory occupying the failure marker name still counts as failed, as the inventory did
            var box = Directory.CreateDirectory(Path.Combine(_root, "box")).FullName;
            File.WriteAllText(Path.Combine(box, "READY"), "");
            Directory.CreateDirectory(Path.Combine(box, "UPLOAD_FAILED"));
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Warning, new System.Text.RegularExpressions.Regex("failure marker is a directory"));
            Assert.AreEqual(PlaytestOutboxBoxKind.Failed, PlaytestOutboxBoxState.Classify(box));
        }
    }
}
