// pin 47f79ca の本番masterでのみ有効な手動ゲート
// Manual gate valid only with the production master pinned at 47f79ca
using Tests.Module.TestMod;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Core.Master;
using Game.MapGeneration.Export;
using Game.MapGeneration.Surface;
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
    [Category("IgnoreCI")]
    public class LegacySurfaceGoldenTest
    {
        private string _scratchRoot;

        [TearDown]
        public void TearDown()
        {
            // 本番マスタを標準入力へ戻す
            // Restore standard inputs so production masters do not leak
            MasterHolder.Load(new MasterJsonFileContainer(ModJsonStringLoader.GetMasterString(
                new ModsResource(Path.Combine(TestModDirectory.ForUnitTestModDirectory, "mods")))));

            if (_scratchRoot != null && Directory.Exists(_scratchRoot)) Directory.Delete(_scratchRoot, true);
        }

        [Test]
        [Timeout(1500000)]
        public void Seed196MatchesCommittedV4Golden()
        {
            // 正本は読むだけ、マスタ指紋も照合
            // Only read the baseline and verify the master fingerprint
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
            Assert.That(fingerprint, Is.EqualTo((string)golden["meta"]["generationMasterFingerprint"]),
                "Legacy manual gate requires production master pin 47f79ca; current generation master fingerprint differs.");

            // 保存された版を明示して実際の選択表を通す
            // Pass the saved revision explicitly through the production dispatch table
            var savedRevision = WorldGeneratorVersion.Resolve((string)golden["meta"]["generatorVersion"], "legacy-golden");
            var config = MapGenerationPipeline.BuildConfig(generation, (int)golden["meta"]["seed"], dataDirectory, savedRevision);
            Assert.That(config.surfaceRevision, Is.EqualTo(WorldSurfaceRevision.Legacy4));
            var run = MapGenerationPipeline.Generate(generation, config);
            Assert.That(run.Output.Resolution, Is.EqualTo((int)golden["meta"]["resolution"]));
            Assert.That(run.Ledger.ComputeDigest(), Is.EqualTo((string)golden["placementLedgerDigest"]));
            Assert.That(run.Ledger.Placements.Count, Is.EqualTo((int)golden["placementLedgerCount"]));
            AssertVector(golden["spawnPoint"], run.Output.SpawnPoint);
            AssertVector(golden["origins"]["noiseOrigin"], run.Output.NoiseOrigin);
            AssertVector(golden["origins"]["sceneOrigin"], run.Output.SceneOrigin);
            AssertVector(golden["origins"]["worldOffset"], new Vector2(run.Config.worldOffsetX, run.Config.worldOffsetZ));
            AssertVector(golden["origins"]["spawnWorldPositionXZ"], run.Config.spawnWorldPosition);

            // 実ライタでr16を出し9枚照合
            // Write r16 with the real writer and compare all nine tiles byte-wise
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

            // 鉱石・液体AABBと旧中心を順序込み照合
            // Compare ordered ore/fluid AABBs and legacy centers
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
