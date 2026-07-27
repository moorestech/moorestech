using System;
using System.IO;
using System.Linq;
using Game.MapGeneration.Provisioning;
using Game.MapGeneration.Transfer;
using Game.Paths;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Game.MapGeneration
{
    // TerrainChunkTotalはワイヤ契約(クライアントが0..N-1を要求する)なので、実ファイルの増減で動かないことを検証する
    // TerrainChunkTotal is a wire contract (clients request 0..N-1), so verify unrelated files never move it
    public class TerrainTransferMetaReaderTest
    {
        private WorldDataDirectory _worldDataDirectory;

        [SetUp]
        public void SetUp()
        {
            var worldRoot = Path.Combine(Path.GetTempPath(), "TerrainTransferMetaReaderTest_" + Guid.NewGuid());
            _worldDataDirectory = WorldDataDirectory.FromWorldRoot(worldRoot);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_worldDataDirectory.Root)) Directory.Delete(_worldDataDirectory.Root, true);
            if (Directory.Exists(_worldDataDirectory.ProvisioningTempDirectory)) Directory.Delete(_worldDataDirectory.ProvisioningTempDirectory, true);
        }

        [Test]
        public void terrainディレクトリに無関係なファイルが混ざってもチャンク総数は変わらない()
        {
            // generated modeの生成はMasterHolderを要求するのでDI構築でマスタをロードする
            // Generated mode requires MasterHolder, so load masters via a DI build first
            new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            WorldProvisioner.EnsureWorld(new WorldProvisionSettings(
                _worldDataDirectory, TestModDirectory.ForUnitTestModDirectory, WorldProvisioner.GeneratedMapMode, 12345));

            var chunkTotalBeforeStrayFile = TerrainTransferMetaReader.Read(_worldDataDirectory).TerrainChunkTotal;
            Assert.Greater(chunkTotalBeforeStrayFile, 0);

            // .DS_Store等の混入で総数がずれるとクライアントが存在しないチャンク番号を要求する。丸ごと1チャンク分の大きさで踏む
            // A stray file (.DS_Store etc.) shifting the total would make clients request a non-existent index; use a full chunk worth
            var strayFilePath = Path.Combine(_worldDataDirectory.TerrainDirectory, ".DS_Store");
            File.WriteAllBytes(strayFilePath, new byte[TerrainTransferMeta.ChunkByteSize]);

            Assert.AreEqual(chunkTotalBeforeStrayFile, TerrainTransferMetaReader.Read(_worldDataDirectory).TerrainChunkTotal);
        }

        [Test]
        public void タイル数が正方格子でなければ並び順が定まらないので例外を投げる()
        {
            Assert.Throws<InvalidOperationException>(() => TerrainTransferMeta.EnumerateTileCoordinates(2));
        }

        [Test]
        public void タイル座標はz行x列の順に列挙される()
        {
            var tileCoordinates = TerrainTransferMeta.EnumerateTileCoordinates(4);

            Assert.AreEqual(new[] { (0, 0), (1, 0), (0, 1), (1, 1) }, tileCoordinates);
        }

        [Test]
        public void 論理ストリームのファイル列はタイル順にheightとbiomeを交互に並べる()
        {
            // この並びがチャンク境界の定義そのもの。取り違えても総バイト数は変わらないためここで固定する
            // This order defines the chunk boundaries; a swap keeps the byte total identical, so pin it here
            var expectedFilePaths = new[]
            {
                _worldDataDirectory.TerrainHeightFilePath(0, 0), _worldDataDirectory.TerrainBiomeFilePath(0, 0),
                _worldDataDirectory.TerrainHeightFilePath(1, 0), _worldDataDirectory.TerrainBiomeFilePath(1, 0),
                _worldDataDirectory.TerrainHeightFilePath(0, 1), _worldDataDirectory.TerrainBiomeFilePath(0, 1),
                _worldDataDirectory.TerrainHeightFilePath(1, 1), _worldDataDirectory.TerrainBiomeFilePath(1, 1),
            };

            Assert.AreEqual(expectedFilePaths, TerrainTransferMeta.EnumerateStreamFilePaths(_worldDataDirectory, 4).ToArray());
        }

        [Test]
        public void タイル数が0以下ならチャンク総数0を返さず例外を投げる()
        {
            // 0は完全平方数なので格子ガードを素通りする。無言でチャンク0本のワイヤ値を返させない
            // Zero is a perfect square and slips past the grid guard; never let it silently yield a zero-chunk wire value
            Assert.Throws<InvalidOperationException>(() => TerrainTransferMeta.EnumerateTileCoordinates(0));
            Assert.Throws<InvalidOperationException>(() => TerrainTransferMeta.EnumerateTileCoordinates(-1));
        }
    }
}
