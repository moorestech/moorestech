using System.IO;
using Game.Map.Interface.Json;
using Game.MapGeneration.Export;
using Game.MapGeneration.Pipeline;
using Game.MapGeneration.Transfer;
using Game.Paths;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Tests.UnitTest.Game.MapGeneration.Surface.Generated
{
    internal static class SavedSurfaceMeta
    {
        internal static GeneratedTerrainTransferMeta WriteAndRead(
            GenerationRun run, WorldDataDirectory saved, string fingerprint, string isolatedId)
        {
            var origins = MapGenerationPipeline.ResolveOrigins(run.Config);
            var world = new WorldMetaJson
            {
                Seed = run.Config.seed,
                GeneratorVersion = WorldGeneratorVersion.Current,
                Algorithm = "VanillaGenerator",
                MapMode = WorldMapMode.Generated,
                CreatedAt = "2026-10-06T00:00:00Z",
                TerrainResolution = run.Output.Resolution,
                TerrainTileCount = run.Output.Tiles.Count,
                TerrainNoiseOriginX = origins.NoiseOrigin.x,
                TerrainNoiseOriginZ = origins.NoiseOrigin.y,
                TerrainSceneOriginX = origins.SceneOrigin.x,
                TerrainSceneOriginZ = origins.SceneOrigin.y,
                GenerationMasterFingerprint = fingerprint,
                PlacementLedgerDigest = run.Ledger.ComputeDigest(),
            };

            // 本番DTOを往復しAABB保存
            // Round-trip the production DTO and reader; persist to map.json
            File.WriteAllText(saved.WorldMetaFilePath, JsonConvert.SerializeObject(world));
            var map = MapInfoJsonBuilder.Build(run.Output);
            File.WriteAllText(saved.MapJsonFilePath, JsonConvert.SerializeObject(map));
            var rereadMap = JsonConvert.DeserializeObject<MapInfoJson>(File.ReadAllText(saved.MapJsonFilePath));
            Assert.That(JsonConvert.SerializeObject(rereadMap), Is.EqualTo(JsonConvert.SerializeObject(map)));
            var loaded = (GeneratedTerrainTransferMeta)TerrainTransferMetaReader.Read(saved);
            Assert.That(loaded.GeneratedPayload.PlacementLedgerDigest, Is.EqualTo(run.Ledger.ComputeDigest()));
            Assert.That(loaded.TerrainChunkTotal, Is.GreaterThan(0));

            // 本番cacheを触らず16桁IDへ隔離
            // Isolate under a 16-digit ID without touching production cache
            return new GeneratedTerrainTransferMeta(isolatedId, loaded.TerrainResolution, loaded.TerrainTileCount,
                loaded.TerrainChunkTotal, loaded.WorldSeed, loaded.GeneratedPayload);
        }
    }
}
