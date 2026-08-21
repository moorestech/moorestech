using System.Linq;
using Core.Master;
using Game.MapGeneration.Identity;
using Game.MapGeneration.Pipeline.Runtime;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Game.MapGeneration.Identity
{
    // 生成マスタ指紋は「同入力なら同値・1文字でも違えば別値」を守らないと、
    // WorldTerrainSessionのfail-fastが誤検知(過検知/見逃し)を起こす
    // The fingerprint must hold "same input -> same value, one differing char -> a different value";
    // otherwise WorldTerrainSession's fail-fast either false-positives or misses drift
    public class GenerationMasterFingerprintTest
    {
        [SetUp]
        public void SetUp()
        {
            // generated modeと同じ経路でMasterHolder.GenerationMasterを実データからロードする
            // Load MasterHolder.GenerationMaster from real data via the same path as generated mode
            new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [Test]
        public void 同じ入力なら同じ指紋になる()
        {
            var selected = MasterHolder.GenerationMaster.SelectedGeneration;
            var jsonText = MasterHolder.GenerationMaster.SourceJsonText;

            var first = GenerationMasterFingerprint.Compute(jsonText, selected, TestModDirectory.ForUnitTestModDirectory);
            var second = GenerationMasterFingerprint.Compute(jsonText, selected, TestModDirectory.ForUnitTestModDirectory);

            Assert.AreEqual(first, second);
        }

        [Test]
        public void JSON原文が1文字違うだけで別の指紋になる()
        {
            var selected = MasterHolder.GenerationMaster.SelectedGeneration;
            var jsonText = MasterHolder.GenerationMaster.SourceJsonText;

            var original = GenerationMasterFingerprint.Compute(jsonText, selected, TestModDirectory.ForUnitTestModDirectory);
            var tampered = GenerationMasterFingerprint.Compute(jsonText + " ", selected, TestModDirectory.ForUnitTestModDirectory);

            Assert.AreNotEqual(original, tampered);
        }

        [Test]
        public void PNGパス列挙順は決定的である()
        {
            var selected = MasterHolder.GenerationMaster.SelectedGeneration;
            var config = GenerationRuntimeConfigFactory.Build(selected);

            var first = PlacementNoiseTextureResolver.EnumerateTexturePngPaths(config).ToList();
            var second = PlacementNoiseTextureResolver.EnumerateTexturePngPaths(config).ToList();

            CollectionAssert.AreEqual(first, second);
        }
    }
}
