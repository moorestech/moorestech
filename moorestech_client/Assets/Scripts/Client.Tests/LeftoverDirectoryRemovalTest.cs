using System.IO;
using Client.Build.Policy;
using NUnit.Framework;

namespace Client.Tests
{
    /// <summary>
    /// 撤去前の成果物（ディレクトリと .meta）を消す口を確かめる（PreRemovalStreamingAssetsCleaner の中身）
    /// Verifies removal of leftover output (directory and .meta), the core of PreRemovalStreamingAssetsCleaner
    /// </summary>
    public class LeftoverDirectoryRemovalTest
    {
        private string _root;

        [SetUp]
        public void CreateRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "leftover-directory-removal-" + Path.GetRandomFileName());
            Directory.CreateDirectory(_root);
        }

        [TearDown]
        public void DeleteRoot()
        {
            Directory.Delete(_root, true);
        }

        [Test]
        public void ディレクトリと_meta_があれば両方消えて_true()
        {
            var target = Path.Combine(_root, "WebUi");
            Directory.CreateDirectory(Path.Combine(target, "dist"));
            File.WriteAllText(Path.Combine(target, "dist", "index.html"), "x");
            File.WriteAllText(target + ".meta", "meta");

            var succeeded = LeftoverDirectoryRemoval.TryRemove(target, out var removedAnything, out var hadMeta, out var failureReason);

            Assert.IsTrue(succeeded, failureReason);
            Assert.IsTrue(removedAnything);
            Assert.IsTrue(hadMeta);
            Assert.IsFalse(Directory.Exists(target), "ディレクトリが残っている");
            Assert.IsFalse(File.Exists(target + ".meta"), ".meta が残っている");
        }

        [Test]
        public void 無ければ何もせず_true()
        {
            var target = Path.Combine(_root, "WebUi");

            var succeeded = LeftoverDirectoryRemoval.TryRemove(target, out var removedAnything, out var hadMeta, out var failureReason);

            Assert.IsTrue(succeeded, failureReason);
            Assert.IsFalse(removedAnything);
            Assert.IsFalse(hadMeta);
        }
    }
}
