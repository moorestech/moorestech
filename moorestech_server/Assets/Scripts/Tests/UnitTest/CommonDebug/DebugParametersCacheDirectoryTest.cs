using System;
using System.IO;
using Common.Debug;
using NUnit.Framework;

namespace Tests.UnitTest.CommonDebug
{
    /// <summary>
    ///     デバッグ設定のキャッシュ先切替が実ファイルI/Oまで届き、環境間で値が漏れないことを検証する
    ///     Verifies that switching the debug parameter cache directory reaches real file I/O and never leaks values across environments
    /// </summary>
    public class DebugParametersCacheDirectoryTest
    {
        private const string TestKey = "DebugParametersCacheDirectoryTest_Flag";

        private string _priorOverride;
        private string _temporaryRoot;

        [SetUp]
        public void CreateTemporaryRoot()
        {
            // このテスト自体が上書きするため、SetUpFixtureが張った隔離先を控えて必ず戻す
            // This test overrides the setting itself, so remember the isolation installed by the SetUpFixture and always restore it
            _priorOverride = DebugParametersCacheDirectory.GetOverride();
            _temporaryRoot = Path.Combine(Path.GetTempPath(), "moorestech-debug-cache-test", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_temporaryRoot);
        }

        [TearDown]
        public void RestoreOverride()
        {
            DebugParametersCacheDirectory.SetOverride(_priorOverride);
            if (Directory.Exists(_temporaryRoot)) Directory.Delete(_temporaryRoot, true);
        }

        [Test]
        public void SavedValueLandsInOverriddenDirectoryAndDoesNotLeakToAnother()
        {
            var firstDirectory = Path.Combine(_temporaryRoot, "first");
            var secondDirectory = Path.Combine(_temporaryRoot, "second");

            // 切替先へ書くと、そのディレクトリに実ファイルが生成され読み戻せる
            // Writing after the switch creates a real file in that directory and reads back
            DebugParametersCacheDirectory.SetOverride(firstDirectory);
            DebugParameters.SaveBool(TestKey, true);
            Assert.IsTrue(File.Exists(Path.Combine(firstDirectory, "BoolDebugParameters.json")));
            Assert.IsTrue(DebugParameters.GetValueOrDefaultBool(TestKey));

            // 別の切替先では既定値に戻る（環境をまたいで値が漏れない）
            // Another target falls back to the default value, proving values do not leak across environments
            DebugParametersCacheDirectory.SetOverride(secondDirectory);
            Assert.IsFalse(DebugParameters.ExistsBool(TestKey));
            Assert.IsFalse(DebugParameters.GetValueOrDefaultBool(TestKey));

            // 元の切替先へ戻せば書いた値が残っている
            // Switching back to the first target still has the written value
            DebugParametersCacheDirectory.SetOverride(firstDirectory);
            Assert.IsTrue(DebugParameters.GetValueOrDefaultBool(TestKey));
        }

        [Test]
        public void CacheDirectoryResolvesOverrideFirstAndFallsBackToDefault()
        {
            DebugParametersCacheDirectory.SetOverride(_temporaryRoot);
            Assert.AreEqual(_temporaryRoot, DebugParametersCacheDirectory.Resolve());

            // 解除すると既定の ../cache 解決へ戻る（書き込みはしないので実キャッシュは不変）
            // Clearing restores the default ../cache resolution (nothing is written, so the real cache stays untouched)
            DebugParametersCacheDirectory.SetOverride(null);
            Assert.AreEqual(Path.GetFullPath("../cache"), DebugParametersCacheDirectory.Resolve());
        }

        [Test]
        public void CopyDefaultCacheToCreatesDestinationAndCopiesOnlyExistingFiles()
        {
            var destination = Path.Combine(_temporaryRoot, "copied");
            DebugParametersCacheDirectory.CopyDefaultTo(destination);
            Assert.IsTrue(Directory.Exists(destination));

            // 既定キャッシュに無いファイルを勝手に作らない（複製元の内容だけが引き継がれる）
            // Never fabricate files the default cache lacks; only the source contents carry over
            var defaultDirectory = Path.GetFullPath("../cache");
            foreach (var fileName in new[] { "BoolDebugParameters.json", "IntDebugParameters.json", "StringDebugParameters.json" })
            {
                var copiedExists = File.Exists(Path.Combine(destination, fileName));
                Assert.AreEqual(File.Exists(Path.Combine(defaultDirectory, fileName)), copiedExists);
            }
        }
    }
}
