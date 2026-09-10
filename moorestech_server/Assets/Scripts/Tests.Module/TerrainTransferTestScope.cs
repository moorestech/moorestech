using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Core.Master;
using Game.MapGeneration.Provisioning;
using Game.MapGeneration.Transfer;
using Game.Paths;
using Mod.Config;
using Mod.Loader;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.Module
{
    /// <summary>
    ///     terrain転送テストが共有する一時ワールドの払い出し・後始末と、転送ペイロードの復元手順を1箇所に集める。
    ///     generatedのプロビジョニングはMasterHolderを要求するため、DI構築でマスタをロードしてから実行する。
    ///     Centralizes the temporary world lifecycle and payload restoration shared by the terrain transfer tests.
    ///     Provisioning in generated mode requires MasterHolder, so masters are loaded via a DI build first.
    /// </summary>
    public sealed class TerrainTransferTestScope
    {
        // 全タイル走査用の低解像度2x2。detailはheightmapに従属するのでheightmapから導く
        // The low-resolution 2x2 used for all-tile traversal; detail is bound to the heightmap, so it is derived from it
        public const int LowResolutionMultiTileGridSide = 2;
        public const int LowResolutionMultiTileHeightmapResolution = 129;
        private const int LowResolutionMultiTileDetailResolution = LowResolutionMultiTileHeightmapResolution - 1;

        private readonly string _label;
        private readonly List<WorldDataDirectory> _createdWorldDataDirectories = new();
        private readonly List<WorldDataDirectory> _createdSharedCacheDirectories = new();

        public TerrainTransferTestScope(string label)
        {
            _label = label;
        }

        // このスコープが払い出したワールドを全て消す。TearDownからのみ呼ぶ
        // Deletes every world handed out by this scope; call it from TearDown only
        public void End()
        {
            // 記録済みcacheだけを回収
            // Delete only caches recorded during provisioning, without re-reading intentionally corrupted metadata in teardown
            foreach (var sharedCacheDirectory in _createdSharedCacheDirectories)
                if (Directory.Exists(sharedCacheDirectory.Root)) Directory.Delete(sharedCacheDirectory.Root, true);
            _createdSharedCacheDirectories.Clear();

            foreach (var worldDataDirectory in _createdWorldDataDirectories)
            {
                if (Directory.Exists(worldDataDirectory.Root)) Directory.Delete(worldDataDirectory.Root, true);
                if (Directory.Exists(worldDataDirectory.ProvisioningTempDirectory)) Directory.Delete(worldDataDirectory.ProvisioningTempDirectory, true);
            }
            _createdWorldDataDirectories.Clear();
        }

        // ディスク上には何も作らずワールドパスだけを払い出す。合成ワールドを手で組むテスト用
        // Hands out a world path with nothing on disk, for tests assembling a synthetic world by hand
        public WorldDataDirectory CreateEmptyWorldDataDirectory()
        {
            var worldRoot = Path.Combine(Path.GetTempPath(), $"{_label}_{Guid.NewGuid()}");
            var worldDataDirectory = WorldDataDirectory.FromWorldRoot(worldRoot);
            _createdWorldDataDirectories.Add(worldDataDirectory);
            return worldDataDirectory;
        }

        public WorldDataDirectory ProvisionGeneratedWorld(int seed)
        {
            // 注意: 実生成は1x1でも重い
            // Warning: even at the 1x1 default this runs generation, file output, and visual prebake; do not multiply per-case calls
            // メタ契約では合成ワールドを使う
            // Use synthetic worlds for metadata, error, and packet contracts; share one fixture snapshot only when real generation is essential
            // generatedの生成はMasterHolderを要求するのでDI構築でマスタをロードする
            // Generated mode requires MasterHolder, so load masters via a DI build first
            new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            return Provision(WorldMapMode.Generated, seed, TestModDirectory.ForUnitTestModDirectory);
        }

        // 全タイル走査だけメモリ上のマスタを低解像度2x2へ差し替える。共有modの高速な1x1既定は動かさない
        // Only the all-tile traversal swaps the in-memory master to a low-resolution 2x2; the shared mod's fast 1x1 default stays put
        public WorldDataDirectory ProvisionLowResolutionMultiTileGeneratedWorld(int seed)
        {
            // マスタは実ファイルではなくMasterHolderが正なので、JObjectを改変してロードするだけで生成へ効く
            // MasterHolder, not the file on disk, is the source generation reads, so loading a modified JObject is enough
            var modsResource = new ModsResource(Path.Combine(TestModDirectory.ForUnitTestModDirectory, "mods"));
            var masterContainer = new MasterJsonFileContainer(ModJsonStringLoader.GetMasterString(modsResource));
            masterContainer.ConfigJsons[0].JsonContents[new JsonFileName("generation")] =
                BuildLowResolutionMultiTileGenerationJson().ToString(Formatting.None);
            MasterHolder.Load(masterContainer);

            return Provision(WorldMapMode.Generated, seed, TestModDirectory.ForUnitTestModDirectory);
        }

        private static JObject BuildLowResolutionMultiTileGenerationJson()
        {
            var generationJsonPath = Path.Combine(
                TestModDirectory.ForUnitTestModDirectory, "mods", "forUnitTest", "master", "generation.json");
            var generationJson = JObject.Parse(File.ReadAllText(generationJsonPath));

            var algorithmParam = (JObject)generationJson["algorithmParam"];
            algorithmParam["gridSizeX"] = LowResolutionMultiTileGridSide;
            algorithmParam["gridSizeZ"] = LowResolutionMultiTileGridSide;
            algorithmParam["overrideResolution"] = LowResolutionMultiTileHeightmapResolution;
            algorithmParam["detailResolution"] = LowResolutionMultiTileDetailResolution;
            return generationJson;
        }

        // 実生成1回分のワールドを複製して払い出す。同一fixture内で同じ生成を繰り返さないための共有スナップショット
        // Hands out a copy of one real generation, the fixture-wide snapshot that keeps repeated cases from regenerating the same world
        public WorldDataDirectory CopyProvisionedWorld(WorldDataDirectory sourceWorldDataDirectory)
        {
            var copiedWorldDataDirectory = CreateEmptyWorldDataDirectory();
            CopyDirectory(sourceWorldDataDirectory.Root, copiedWorldDataDirectory.Root);
            return copiedWorldDataDirectory;
        }

        public WorldDataDirectory ProvisionTemplateWorld(int seed)
        {
            return Provision(WorldMapMode.Template, seed, TestModDirectory.ForUnitTestModDirectory);
        }

        // チャンクペイロードの圧縮形式はTerrainChunkReaderと対で決まるので、解凍側もここで一本化する
        // The chunk payload's compression format is pinned together with TerrainChunkReader, so decompression lives here too
        public static byte[] DecompressChunk(byte[] compressedBytes)
        {
            using var decompressedStream = new MemoryStream();
            using (var gzipStream = new GZipStream(new MemoryStream(compressedBytes), CompressionMode.Decompress))
                gzipStream.CopyTo(decompressedStream);
            return decompressedStream.ToArray();
        }

        // 与えられた順のままファイルを連結する。並び順は各テストが自前で書く(実装の列挙を使うと検証が循環する)
        // Concatenates files in the given order; each test spells the order out itself, since reusing the production enumerator would be circular
        public static byte[] ReadFilesInOrder(IReadOnlyList<string> filePaths)
        {
            return filePaths.SelectMany(File.ReadAllBytes).ToArray();
        }

        // 実ディレクトリだけを再帰コピー
        // Recursively copies only real directories handed out by tests; symbolic links are not supported
        public static void CopyDirectory(string sourceDirectory, string destinationDirectory)
        {
            Directory.CreateDirectory(destinationDirectory);
            foreach (var filePath in Directory.GetFiles(sourceDirectory))
                File.Copy(filePath, Path.Combine(destinationDirectory, Path.GetFileName(filePath)), true);
            foreach (var subDirectory in Directory.GetDirectories(sourceDirectory))
                CopyDirectory(subDirectory, Path.Combine(destinationDirectory, Path.GetFileName(subDirectory)));
        }

        private WorldDataDirectory Provision(string mapMode, int seed, string serverDataDirectory)
        {
            var worldDataDirectory = CreateEmptyWorldDataDirectory();
            try
            {
                WorldProvisioner.EnsureWorld(new WorldProvisionSettings(
                    worldDataDirectory, serverDataDirectory, mapMode, seed));
                return worldDataDirectory;
            }
            finally
            {
                // 確定メタのcacheを失敗時も登録
                // Register a shared cache from committed metadata even when provisioning fails afterward
                if (File.Exists(worldDataDirectory.WorldMetaFilePath))
                {
                    var meta = TerrainTransferMetaReader.Read(worldDataDirectory);
                    _createdSharedCacheDirectories.Add(WorldDataDirectory.ForWorldCache(meta.WorldId));
                }
            }
        }
    }
}
