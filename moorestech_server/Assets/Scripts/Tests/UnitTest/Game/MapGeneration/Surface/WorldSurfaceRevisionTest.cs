using System;
using Game.MapGeneration.Facade.Surface;
using Game.MapGeneration.Pipeline;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Transfer;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.UnitTest.Game.MapGeneration.Surface
{
    public class WorldSurfaceRevisionTest
    {
        [TestCase("4.0.0", WorldSurfaceRevision.Legacy4)]
        [TestCase("5.0.0", WorldSurfaceRevision.Grounded5)]
        public void SupportedWorldLoads(string version, WorldSurfaceRevision revision)
        {
            Assert.DoesNotThrow(() => WorldGeneratorVersion.ThrowIfUnsupported(version, "fixture"));
            Assert.That(WorldGeneratorVersion.Resolve(version, "fixture"), Is.EqualTo(revision));

            // 転送境界も旧版を受理し保存値を維持する
            // The transfer boundary accepts the legacy revision and retains the saved value
            var meta = (GeneratedTerrainTransferMeta)TerrainTransferMeta.FromWire(
                WorldMapMode.Generated, "fixture", 33, 1, 1, 196,
                new TerrainOrigins(Vector2.zero, Vector2.zero), "fingerprint", version, "digest");
            Assert.That(meta.GeneratedPayload.GeneratorVersion, Is.EqualTo(version));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("6.0.0")]
        [TestCase("4.0")]
        public void UnknownRevisionCannotRegenerateAsCurrent(string version)
        {
            Assert.That(WorldGeneratorVersion.Supports(version), Is.False);
            LogAssert.Expect(LogType.Error, $"Unsupported generator '{version}' for world 'fixture'; connect to a server on the same build.");
            Assert.Throws<InvalidOperationException>(() => WorldGeneratorVersion.Resolve(version, "fixture"));
        }

        [Test]
        public void NewConfigurationUsesGroundedRevision()
        {
            Assert.That(new TerrainGenerationConfig().surfaceRevision, Is.EqualTo(WorldSurfaceRevision.Grounded5));
            Assert.That(WorldGeneratorVersion.Current, Is.EqualTo("5.0.0"));
        }

        [TestCase(WorldSurfaceRevision.Legacy4, typeof(TerrainSurfacePresentation.Existing))]
        [TestCase(WorldSurfaceRevision.Grounded5, typeof(TerrainSurfacePresentation.Grounded))]
        public void PresentationUsesTheGenerationRevisionRegistry(WorldSurfaceRevision revision, Type presentation)
        {
            var config = new TerrainGenerationConfig { surfaceRevision = revision };
            var policy = MapGenerationAlgorithmTable.ResolveSurface(revision).CreateHeightPolicy(config);
            Assert.That(policy.Presentation, Is.TypeOf(presentation));
        }

        [Test]
        public void UnknownPresentationRevisionFailsBeforeBaking()
        {
            LogAssert.Expect(LogType.Error, "Unsupported surface revision '999'.");
            Assert.Throws<InvalidOperationException>(() => MapGenerationAlgorithmTable.ResolveSurface((WorldSurfaceRevision)999));
        }

        [TestCase("4.0.0")]
        [TestCase("5.0.0")]
        public void SavedMetadataAcceptsBothSupportedVersions(string version)
        {
            // readerのnullable理由契約を両版で固定する
            // Pin the reader's nullable reason contract for both revisions
            var meta = new WorldMetaJson
            {
                MapMode = WorldMapMode.Generated,
                GeneratorVersion = version,
                GenerationMasterFingerprint = "fingerprint",
                PlacementLedgerDigest = "digest",
                TerrainNoiseOriginX = 0f,
                TerrainNoiseOriginZ = 0f,
                TerrainSceneOriginX = 0f,
                TerrainSceneOriginZ = 0f
            };
            Assert.That(TerrainTransferMetaReader.DescribeGeneratedMetaProblem(meta), Is.Null);
        }
    }
}
