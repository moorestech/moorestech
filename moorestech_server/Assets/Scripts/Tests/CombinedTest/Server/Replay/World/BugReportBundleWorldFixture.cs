using System.IO;
using Game.MapGeneration.Transfer;
using Game.Paths;
using Newtonsoft.Json;

namespace Tests.CombinedTest.Server.Replay.World
{
    // 箱・サーバーデータ・同梱スナップショットを一時ディレクトリに組む。実ユーザーの共有キャッシュには何も書かない
    // Builds a box, server data and bundled snapshots under a temp directory; nothing is ever written into the real user's shared cache
    public sealed class BugReportBundleWorldFixture
    {
        public readonly string Root;

        public BugReportBundleWorldFixture()
        {
            Root = Path.Combine(Path.GetTempPath(), "bwr-" + Path.GetRandomFileName());
            Directory.CreateDirectory(Root);
        }

        public void Delete()
        {
            Directory.Delete(Root, true);
        }

        // 指紋は毎回ランダムにする。固定値だと実マシンの共有キャッシュに同じ worldId があるだけで結果が変わる
        // The fingerprint is random every time; a fixed one lets the real machine's shared cache change the outcome through a matching worldId
        public static string RandomFingerprint()
        {
            return "fp-" + Path.GetRandomFileName();
        }

        public static WorldMetaJson Meta(string mode, string fingerprint, string digest)
        {
            return new WorldMetaJson
            {
                Seed = 196, GeneratorVersion = WorldGeneratorVersion.Current, Algorithm = "VanillaGenerator", MapMode = mode,
                GenerationMasterFingerprint = fingerprint, PlacementLedgerDigest = digest,
                TerrainResolution = 2049, TerrainTileCount = 9,
                TerrainNoiseOriginX = 0f, TerrainNoiseOriginZ = 0f, TerrainSceneOriginX = 0f, TerrainSceneOriginZ = 0f,
            };
        }

        public string Bundle(WorldMetaJson meta, bool withMapJson)
        {
            var world = WorldDataDirectory.FromWorldRoot(Path.Combine(Root, "bundle", "world"));
            Directory.CreateDirectory(world.Root);
            File.WriteAllText(world.WorldMetaFilePath, JsonConvert.SerializeObject(meta));
            if (withMapJson) File.WriteAllText(world.MapJsonFilePath, "{}");
            return Path.Combine(Root, "bundle");
        }

        // ワイヤの語をそのまま書く。未知の語や壊れた manifest も同じ口で作れるようにする
        // Writes the wire word verbatim, so unknown words and broken manifests come from the same helper
        public static void WriteManifest(string bundle, string manifestJson)
        {
            File.WriteAllText(Path.Combine(bundle, BugReportBundleLayout.ManifestFileName), manifestJson);
        }

        public static void WriteManifestDeclaring(string bundle, string worldDefinitionWireWord)
        {
            WriteManifest(bundle, "{\"schemaVersion\":2,\"worldDefinition\":\"" + worldDefinitionWireWord + "\"}");
        }

        public static WorldDataDirectory WriteBundledSnapshot(string serverData, WorldMetaJson bundleMeta, WorldMetaJson snapshotMeta, bool withTerrain)
        {
            var worldId = WorldIdentity.CalculateGenerated(bundleMeta.Seed, bundleMeta.GenerationMasterFingerprint, bundleMeta.GeneratorVersion);
            var snapshot = WorldDataDirectory.ForBundledSnapshot(serverData, worldId);
            Directory.CreateDirectory(snapshot.Root);
            File.WriteAllText(snapshot.WorldMetaFilePath, JsonConvert.SerializeObject(snapshotMeta));
            File.WriteAllText(snapshot.MapJsonFilePath, "{\"snapshot\":true}");
            if (withTerrain)
            {
                Directory.CreateDirectory(Path.Combine(snapshot.TerrainDirectory, "tiles"));
                File.WriteAllBytes(Path.Combine(snapshot.TerrainDirectory, "height_0_0.r16"), new byte[8]);
                File.WriteAllBytes(Path.Combine(snapshot.TerrainDirectory, "tiles", "tile_0.bin"), new byte[4]);
            }
            return snapshot;
        }

        public string ServerData()
        {
            var dir = Path.Combine(Root, "serverData");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }
}
