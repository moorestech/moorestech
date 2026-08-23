using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Game.MapGeneration.Provisioning;
using Game.MapGeneration.Transfer;
using Game.Paths;
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
        private readonly string _label;
        private readonly List<WorldDataDirectory> _createdWorldDataDirectories = new();
        private readonly List<string> _createdServerDataDirectories = new();

        public TerrainTransferTestScope(string label)
        {
            _label = label;
        }

        // このスコープが払い出したワールドを全て消す。TearDownからのみ呼ぶ
        // Deletes every world handed out by this scope; call it from TearDown only
        public void End()
        {
            foreach (var worldDataDirectory in _createdWorldDataDirectories)
            {
                // 確定メタの共有cacheを失敗時も回収
                // Remove the shared cache referenced by committed metadata even when provisioning fails partway through
                if (File.Exists(worldDataDirectory.WorldMetaFilePath))
                {
                    var meta = TerrainTransferMetaReader.Read(worldDataDirectory);
                    var shared = WorldDataDirectory.ForWorldCache(meta.WorldId);
                    if (Directory.Exists(shared.Root)) Directory.Delete(shared.Root, true);
                }
                if (Directory.Exists(worldDataDirectory.Root)) Directory.Delete(worldDataDirectory.Root, true);
                if (Directory.Exists(worldDataDirectory.ProvisioningTempDirectory)) Directory.Delete(worldDataDirectory.ProvisioningTempDirectory, true);
            }
            _createdWorldDataDirectories.Clear();

            foreach (var serverDataDirectory in _createdServerDataDirectories)
                if (Directory.Exists(serverDataDirectory)) Directory.Delete(serverDataDirectory, true);
            _createdServerDataDirectories.Clear();
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

        // 全タイル用modで1x1既定値を維持
        // Creates a dedicated low-resolution mod for all-tile traversal without changing the shared test mod's fast 1x1 default
        public WorldDataDirectory ProvisionGeneratedWorld(int seed, int gridSide, int heightmapResolution)
        {
            var serverDataDirectory = Path.Combine(Path.GetTempPath(), $"{_label}_ServerData_{Guid.NewGuid()}");
            _createdServerDataDirectories.Add(serverDataDirectory);
            CopyDirectory(TestModDirectory.ForUnitTestModDirectory, serverDataDirectory);

            var generationJsonPath = Path.Combine(serverDataDirectory, "mods", "forUnitTest", "master", "generation.json");
            var generationJson = JObject.Parse(File.ReadAllText(generationJsonPath));
            var algorithmParam = (JObject)generationJson["algorithmParam"];
            algorithmParam["gridSizeX"] = gridSide;
            algorithmParam["gridSizeZ"] = gridSide;
            algorithmParam["overrideResolution"] = heightmapResolution;
            File.WriteAllText(generationJsonPath, generationJson.ToString());

            new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(serverDataDirectory));
            return Provision(WorldMapMode.Generated, seed, serverDataDirectory);
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
            WorldProvisioner.EnsureWorld(new WorldProvisionSettings(
                worldDataDirectory, serverDataDirectory, mapMode, seed));
            return worldDataDirectory;
        }
    }
}
