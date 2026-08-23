using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Game.MapGeneration.Provisioning;
using Game.MapGeneration.Transfer;
using Game.Paths;
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
            // generatedの生成はMasterHolderを要求するのでDI構築でマスタをロードする
            // Generated mode requires MasterHolder, so load masters via a DI build first
            new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            return Provision(WorldMapMode.Generated, seed);
        }

        public WorldDataDirectory ProvisionTemplateWorld(int seed)
        {
            return Provision(WorldMapMode.Template, seed);
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

        private WorldDataDirectory Provision(string mapMode, int seed)
        {
            var worldDataDirectory = CreateEmptyWorldDataDirectory();
            WorldProvisioner.EnsureWorld(new WorldProvisionSettings(
                worldDataDirectory, TestModDirectory.ForUnitTestModDirectory, mapMode, seed));
            return worldDataDirectory;
        }
    }
}
