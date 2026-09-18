using System.IO;
using Client.PlaytestReceiver.Http;
using Client.PlaytestReceiver.Upload;
using NUnit.Framework;

namespace Client.Tests.PlaytestReceiver
{
    // 宣言の世代（F12）。受け口は同じ世代の食い違いと拡大を拒むので、縮めたときだけ世代を上げる
    // The declaration's generation (F12); the receiver refuses a same-generation mismatch or growth, so only a shrink bumps it
    public class PlaytestUploadDeclaredRecordTest
    {
        private string _box;

        [SetUp]
        public void CreateBox()
        {
            _box = Path.Combine(Path.GetTempPath(), "playtest-declared-" + Path.GetRandomFileName());
            Directory.CreateDirectory(_box);
        }

        [TearDown]
        public void DeleteBox()
        {
            Directory.Delete(_box, true);
        }

        [Test]
        public void 初回は世代1で同じ集合なら据え置き縮めれば1つ上げる()
        {
            Assert.AreEqual(1, PlaytestUploadDeclaredRecord.Declare(_box, Files("manifest.json", "a.bin")));
            Assert.AreEqual(1, PlaytestUploadDeclaredRecord.Declare(_box, Files("a.bin", "manifest.json")));
            Assert.AreEqual(2, PlaytestUploadDeclaredRecord.Declare(_box, Files("manifest.json")));
            Assert.AreEqual(2, PlaytestUploadDeclaredRecord.Declare(_box, Files("manifest.json")));

            var record = PlaytestUploadDeclaredRecord.Read(_box);
            Assert.AreEqual(2, record.Generation);
            CollectionAssert.AreEquivalent(new[] { "manifest.json" }, record.Paths);
        }

        [Test]
        public void 一度も宣言していない箱の記録は無い()
        {
            Assert.IsNull(PlaytestUploadDeclaredRecord.Read(_box));
        }

        // 世代の行が無い旧形式（1行1パス）は世代1として読み、縮めれば世代2で送る
        // The older form without a generation line (one path per line) reads as generation 1, and a shrink goes out as generation 2
        [Test]
        public void 世代の行が無い旧形式の記録は世代1として読む()
        {
            File.WriteAllLines(Path.Combine(_box, PlaytestOutboxScanner.DeclaredMarker), new[] { "manifest.json", "a.bin" });

            var record = PlaytestUploadDeclaredRecord.Read(_box);
            Assert.AreEqual(1, record.Generation);
            CollectionAssert.AreEquivalent(new[] { "manifest.json", "a.bin" }, record.Paths);
            Assert.AreEqual(2, PlaytestUploadDeclaredRecord.Declare(_box, Files("manifest.json")));
        }

        private static PlaytestDeclaredFile[] Files(params string[] paths)
        {
            var files = new PlaytestDeclaredFile[paths.Length];
            for (var i = 0; i < paths.Length; i++) files[i] = new PlaytestDeclaredFile(paths[i], 1, Path.Combine("/box", paths[i]));
            return files;
        }
    }
}
