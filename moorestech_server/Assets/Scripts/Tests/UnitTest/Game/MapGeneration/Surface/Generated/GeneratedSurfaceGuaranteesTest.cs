using Tests.Module.TestMod;
using Mod.Loader;
using Mod.Config;
using System.IO;
using Core.Master;
using System.Collections;
using NUnit.Framework;

namespace Tests.UnitTest.Game.MapGeneration.Surface.Generated
{
    [Category("IgnoreCI")]
    public class GeneratedSurfaceGuaranteesTest
    {
        [TearDown]
        public void TearDown()
        {
            // 本番マスタを後続テストへ残さず標準テスト入力へ戻す
            // Restore standard test inputs so production masters do not leak into later tests
            MasterHolder.Load(new MasterJsonFileContainer(ModJsonStringLoader.GetMasterString(
                new ModsResource(Path.Combine(TestModDirectory.ForUnitTestModDirectory, "mods")))));
        }

        public static IEnumerable Cases()
        {
            // 本番パラメータを保ち、seedと格子寸法を直交検査する
            // Retain production parameters while checking seeds against grid dimensions
            foreach (int seed in new[] { 196, 1, 2, 42, 197 })
            foreach (int tiles in new[] { 1, 3 })
                yield return new TestCaseData(seed, tiles, 1000f, 1000f);
            foreach (int seed in new[] { 196, 1, 2, 42, 197 })
                yield return new TestCaseData(seed, 3, 1000f, 800f);
        }

        [TestCaseSource(nameof(Cases))]
        [Timeout(1500000)]
        public void ProductionGenerationRetainsGuaranteesAfterSaveAndVisualCacheReload(
            int seed, int tiles, float width, float length)
        {
            using var fixture = new GeneratedSurfaceFixture(seed, tiles, width, length);
            GeneratedSurfaceValidation.Check(fixture, true);
        }
    }

    [Category("IgnoreCI")]
    [Category("HeavyGeneratedSurface")]
    public class GeneratedSurfaceGuaranteesHeavyTest
    {
        [TearDown]
        public void TearDown()
        {
            // 本番マスタを後続テストへ残さず標準テスト入力へ戻す
            // Restore standard test inputs so production masters do not leak into later tests
            MasterHolder.Load(new MasterJsonFileContainer(ModJsonStringLoader.GetMasterString(
                new ModsResource(Path.Combine(TestModDirectory.ForUnitTestModDirectory, "mods")))));
        }

        [TestCase(196)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(42)]
        [TestCase(197)]
        // 本番3x3を超える負荷検査で、全検査を保ったまま1件25分を超えるため60分にする
        // Stress check beyond production 3x3 exceeds 25 min per case with all checks kept, so allow 60 min
        [Timeout(3600000)]
        public void Production2049FiveByFiveRetainsGuarantees(int seed)
        {
            // 最大fixtureは専用filterで単独実行する
            // Run the largest fixture alone through its dedicated filter
            using var fixture = new GeneratedSurfaceFixture(seed, 5, 1000f, 1000f);
            GeneratedSurfaceValidation.Check(fixture, true);
        }
    }
}
