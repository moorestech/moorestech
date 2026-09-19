using System.IO;
using Game.MapGeneration.Transfer;
using Game.Paths;
using Newtonsoft.Json;
using NUnit.Framework;
using Server.Boot.Replay.World;

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
                TerrainResolution = 2049, TerrainTileCount = 1,
                TerrainNoiseOriginX = 0f, TerrainNoiseOriginZ = 0f, TerrainSceneOriginX = 0f, TerrainSceneOriginZ = 0f,
            };
        }

        public string Bundle(WorldMetaJson meta, bool withMapJson)
        {
            var world = WorldDataDirectory.FromWorldRoot(Path.Combine(Root, "bundle", "world"));
            Directory.CreateDirectory(world.Root);
            File.WriteAllText(world.WorldMetaFilePath, JsonConvert.SerializeObject(meta));
            if (withMapJson) File.WriteAllText(world.MapJsonFilePath, "{}");

            // 既定は worldDefinition を宣言しない旧版の manifest。宣言を試すテストは WriteManifestDeclaring で上書きする
            // Defaults to an old manifest that declares no worldDefinition; declaration tests overwrite it with WriteManifestDeclaring
            var bundle = Path.Combine(Root, "bundle");
            WriteManifest(bundle, "{\"schemaVersion\":2}");
            return bundle;
        }

        // ワイヤの語をそのまま書く。未知の語や壊れた manifest も同じ口で作れるようにする
        // Writes the wire word verbatim, so unknown words and broken manifests come from the same helper
        public static void WriteManifest(string bundle, string manifestJson)
        {
            File.WriteAllText(Path.Combine(bundle, BugReportBundleLayout.ManifestFileName), manifestJson);
        }

        public static void WriteManifestDeclaring(string bundle, string worldDefinitionWireWord)
        {
            WriteManifest(bundle, "{\"schemaVersion\":3,\"worldDefinition\":\"" + worldDefinitionWireWord + "\"}");
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
                // 宣言したタイル数どおりの height を置く。並べられないタイル数（不正値のテスト）では terrain/ だけを作る
                // Writes a height per declared tile; for a count that cannot be laid out (the invalid-value tests) only terrain/ is created
                Directory.CreateDirectory(Path.Combine(snapshot.TerrainDirectory, "tiles"));
                if (TerrainTransferMeta.DescribeTileCountProblem(snapshotMeta.TerrainTileCount) == null)
                {
                    foreach (var tilePath in TerrainTransferMeta.EnumerateStreamFilePaths(snapshot, snapshotMeta.TerrainTileCount)) File.WriteAllBytes(tilePath, new byte[8]);
                }
                File.WriteAllBytes(Path.Combine(snapshot.TerrainDirectory, "tiles", "tile_0.bin"), new byte[4]);
            }
            return snapshot;
        }

        public static WorldDataDirectory ResolvedWorld(BugReportBundleWorldResolution resolution)
        {
            Assert.IsTrue(resolution.TryGetWorld(out var world, out var rejectedReason), $"解決されるはずが拒否された: {rejectedReason}");
            return world;
        }

        public static string RejectedReason(BugReportBundleWorldResolution resolution)
        {
            Assert.IsFalse(resolution.TryGetWorld(out var world, out var rejectedReason), $"拒否されるはずが解決された: {world?.Root}");
            return rejectedReason;
        }

        public string ServerData()
        {
            var dir = Path.Combine(Root, "serverData");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }
}
