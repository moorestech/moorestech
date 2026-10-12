using System;
using System.IO;
using Game.Map.Interface.Json;
using Game.MapGeneration.Provisioning;
using Game.MapGeneration.Transfer;
using Game.Paths;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Game.MapGeneration.Provisioning
{
    // drift系テストが共有する、一時ワールドの生成・記録の読み書き・後片付け
    // Temporary-world provisioning, record read/write and cleanup shared by the drift tests
    internal sealed class GeneratedWorldProvisionFixture
    {
        private const int WorldSeed = 12345;

        public readonly WorldDataDirectory WorldDataDirectory;

        internal GeneratedWorldProvisionFixture(string testName)
        {
            var worldRoot = Path.Combine(Path.GetTempPath(), testName + "_" + Guid.NewGuid());
            WorldDataDirectory = WorldDataDirectory.FromWorldRoot(worldRoot);
        }

        internal void DeleteWorld()
        {
            if (Directory.Exists(WorldDataDirectory.Root)) Directory.Delete(WorldDataDirectory.Root, true);
            if (Directory.Exists(WorldDataDirectory.ProvisioningTempDirectory)) Directory.Delete(WorldDataDirectory.ProvisioningTempDirectory, true);
        }

        // generated modeはMasterHolder.GenerationMaster.SelectedGenerationを要求するため、ForUnitTest modをDIコンテナ生成経由でロードする
        // generated mode requires MasterHolder.GenerationMaster.SelectedGeneration, so the ForUnitTest mod is loaded via DI container generation
        internal WorldProvisionSettings ProvisionGeneratedWorld()
        {
            new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            return ProvisionGeneratedWorldWithLoadedMaster();
        }

        internal WorldProvisionSettings ProvisionGeneratedWorldWithLoadedMaster()
        {
            var settings = new WorldProvisionSettings(WorldDataDirectory, TestModDirectory.ForUnitTestModDirectory, WorldMapMode.Generated, WorldSeed);
            WorldProvisioner.EnsureWorld(settings);
            return settings;
        }

        internal WorldMetaJson ReadWorldMeta()
        {
            return JsonConvert.DeserializeObject<WorldMetaJson>(File.ReadAllText(WorldDataDirectory.WorldMetaFilePath));
        }

        internal MapInfoJson ReadMapInfo()
        {
            return JsonConvert.DeserializeObject<MapInfoJson>(File.ReadAllText(WorldDataDirectory.MapJsonFilePath));
        }

        internal void WriteMapInfo(MapInfoJson mapInfoJson)
        {
            File.WriteAllText(WorldDataDirectory.MapJsonFilePath, JsonConvert.SerializeObject(mapInfoJson, Formatting.Indented));
        }

        // 指紋と版以外は1文字も動かさない。既定の日付解釈はcreatedAtを末尾0の落ちた別表記で書き戻す
        // Not one character outside the fingerprint and version may move: the default date handling rewrites createdAt with its trailing zeros trimmed
        // createdAtはworldId(seedと繋いだ文字列のハッシュ)の素材で、動くと共有キャッシュの宛先が別ワールドへ移る
        // createdAt feeds the worldId (a hash of it joined with the seed), so moving it sends the shared cache's destination to another world
        internal void WriteFingerprintAndGeneratorVersion(string fingerprint, string generatorVersion)
        {
            var keepDatesAsText = new JsonSerializerSettings { DateParseHandling = DateParseHandling.None };
            var worldMeta = JsonConvert.DeserializeObject<JObject>(File.ReadAllText(WorldDataDirectory.WorldMetaFilePath), keepDatesAsText);
            worldMeta["generationMasterFingerprint"] = fingerprint;
            worldMeta["generatorVersion"] = generatorVersion;
            File.WriteAllText(WorldDataDirectory.WorldMetaFilePath, worldMeta.ToString());
        }

        // 共有キャッシュはワールドディレクトリの外なのでDeleteWorldの対象外。worldIdが分かるテストが自分で片付ける
        // The shared cache lives outside the world directory and escapes DeleteWorld, so a test that knows the worldId cleans it up itself
        internal static void DeleteSharedWorldCache(string worldId)
        {
            var sharedCacheRoot = WorldDataDirectory.ForWorldCache(worldId).Root;
            if (Directory.Exists(sharedCacheRoot)) Directory.Delete(sharedCacheRoot, true);
        }
    }
}
