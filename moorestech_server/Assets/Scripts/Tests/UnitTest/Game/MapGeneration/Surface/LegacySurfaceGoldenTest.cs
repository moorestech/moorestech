using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Core.Master;
using Game.MapGeneration.Export;
using Game.MapGeneration.Facade.Surface;
using Game.MapGeneration.Identity;
using Game.MapGeneration.Pipeline;
using Game.MapGeneration.Transfer;
using Game.Paths;
using Mod.Config;
using Mod.Loader;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Tests.UnitTest.Game.MapGeneration.Surface
{
    public class LegacySurfaceGoldenTest
    {
        private string _scratchRoot;

        [TearDown]
        public void TearDown()
        {
            if (_scratchRoot != null && Directory.Exists(_scratchRoot)) Directory.Delete(_scratchRoot, true);
        }

        [Test]
        public void Seed196MatchesCommittedV4Golden()
        {
            // 採取済み正本は読むだけとし本番マスタの指紋も照合する
            // Read the committed baseline without rewriting it and verify the production master fingerprint
            var repository = Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
            var fixturePath = Path.Combine(repository,
                "moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/Surface/Fixtures/legacy-v4-seed196.json");
            var golden = JObject.Parse(File.ReadAllText(fixturePath));
            var dataDirectory = Path.GetFullPath(Path.Combine(repository, (string)golden["meta"]["serverDataDirectory"]));
            Assert.That(Directory.Exists(Path.Combine(dataDirectory, "mods")), Is.True, "Pinned production mod must be provisioned");
            var resources = new ModsResource(Path.Combine(dataDirectory, "mods"));
            MasterHolder.Load(new MasterJsonFileContainer(ModJsonStringLoader.GetMasterString(resources)));
            var generation = MasterHolder.GenerationMaster.SelectedGeneration;
            var fingerprint = GenerationMasterFingerprint.Compute(MasterHolder.GenerationMaster.SourceJsonText, generation, dataDirectory);
            Assert.That(fingerprint, Is.EqualTo((string)golden["meta"]["generationMasterFingerprint"]));

            // 保存された版を明示して実際の選択表を通す
            // Pass the saved revision explicitly through the production dispatch table
            var config = MapGenerationPipeline.BuildConfig(generation, (int)golden["meta"]["seed"], dataDirectory);
            config.SurfaceRevision = WorldGeneratorVersion.Resolve((string)golden["meta"]["generatorVersion"], "legacy-golden");
            Assert.That(config.SurfaceRevision, Is.EqualTo(WorldSurfaceRevision.Legacy4));
            var run = MapGenerationPipeline.Generate(generation, config);
            Assert.That(run.Output.Resolution, Is.EqualTo((int)golden["meta"]["resolution"]));
            Assert.That(run.Ledger.ComputeDigest(), Is.EqualTo((string)golden["placementLedgerDigest"]));
            Assert.That(run.Ledger.Placements.Count, Is.EqualTo((int)golden["placementLedgerCount"]));
            AssertVector(golden["spawnPoint"], run.Output.SpawnPoint);
            AssertVector(golden["origins"]["noiseOrigin"], run.Output.NoiseOrigin);
            AssertVector(golden["origins"]["sceneOrigin"], run.Output.SceneOrigin);
            AssertVector(golden["origins"]["worldOffset"], new Vector2(run.Config.worldOffsetX, run.Config.worldOffsetZ));
            AssertVector(golden["origins"]["spawnWorldPositionXZ"], run.Config.spawnWorldPosition);

            // 実際の保存ライタでr16を出力し9枚すべてのbyteを照合する
            // Use the production writer to compare every byte through the hashes of all nine r16 tiles
            _scratchRoot = Path.Combine(Path.GetTempPath(), "LegacySurfaceGolden_" + Guid.NewGuid());
            var directory = WorldDataDirectory.FromWorldRoot(_scratchRoot);
            TerrainFileWriter.Write(directory, run.Output);
            Assert.That(run.Output.Tiles.Count, Is.EqualTo(golden["tiles"].Count()));
            foreach (var tile in golden["tiles"])
            {
                var bytes = File.ReadAllBytes(directory.TerrainHeightFilePath((int)tile["tileX"], (int)tile["tileZ"]));
                Assert.That(bytes.Length, Is.EqualTo((int)tile["byteLength"]));
                using var sha = SHA256.Create();
                var hash = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
                Assert.That(hash, Is.EqualTo((string)tile["sha256"]), (string)tile["fileName"]);
            }

            // 順序も含め鉱石・液体AABBと旧表示中心を照合する
            // Compare ordered item/fluid AABBs and the legacy presentation centers
            AssertVeins(golden["itemVeins"], run.Output.ItemVeins.ToArray());
            AssertVeins(golden["fluidVeins"], run.Output.FluidVeins.ToArray());
            var veins = run.Output.ItemVeins.Concat(run.Output.FluidVeins).ToArray();
            var roots = golden["outcropRoots"]["entries"].ToArray();
            Assert.That(veins.Length, Is.EqualTo(roots.Length));
            for (var index = 0; index < veins.Length; index++)
            {
                var vein = veins[index];
                Assert.That(vein.VeinGuid, Is.EqualTo((string)roots[index]["guid"]));
                AssertVector(roots[index]["root"], (Vector3)(vein.Min + vein.Max + Vector3Int.one) * 0.5f);
            }
        }

        private static void AssertVeins(JToken expected, PlacedVein[] actual)
        {
            Assert.That(actual.Length, Is.EqualTo(expected.Count()));
            for (var index = 0; index < actual.Length; index++)
            {
                Assert.That(actual[index].VeinGuid, Is.EqualTo((string)expected[index]["guid"]));
                AssertVector(expected[index]["min"], actual[index].Min);
                AssertVector(expected[index]["max"], actual[index].Max);
            }
        }

        private static void AssertVector(JToken expected, Vector3 actual)
        {
            Assert.That(actual.x, Is.EqualTo(Parse(expected[0])));
            Assert.That(actual.y, Is.EqualTo(Parse(expected[1])));
            if (expected.Count() == 3) Assert.That(actual.z, Is.EqualTo(Parse(expected[2])));
        }

        private static float Parse(JToken value)
        {
            return float.Parse(value.ToString(), CultureInfo.InvariantCulture);
        }
    }
}
