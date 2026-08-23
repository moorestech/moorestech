using System;
using System.IO;
using System.Linq;
using Game.MapGeneration.Export;
using Game.MapGeneration.Transfer;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Tests.Module;
using UnityEngine;

namespace Tests.UnitTest.Game.MapGeneration
{
    // TerrainChunkTotalはワイヤ契約(クライアントが0..N-1を要求する)なので、実ファイルの増減で動かないことを検証する
    // TerrainChunkTotal is a wire contract (clients request 0..N-1), so verify unrelated files never move it
    public class TerrainTransferMetaReaderTest
    {
        private TerrainTransferTestScope _testScope;

        [SetUp]
        public void SetUp()
        {
            _testScope = new TerrainTransferTestScope(nameof(TerrainTransferMetaReaderTest));
        }

        [TearDown]
        public void TearDown()
        {
            _testScope.End();
        }

        [Test]
        public void terrainディレクトリに無関係なファイルが混ざってもチャンク総数は変わらない()
        {
            var worldDataDirectory = _testScope.ProvisionGeneratedWorld(12345);

            var chunkTotalBeforeStrayFile = TerrainTransferMetaReader.Read(worldDataDirectory).TerrainChunkTotal;
            Assert.Greater(chunkTotalBeforeStrayFile, 0);

            // .DS_Store等の混入で総数がずれるとクライアントが存在しないチャンク番号を要求する。丸ごと1チャンク分の大きさで踏む
            // A stray file (.DS_Store etc.) shifting the total would make clients request a non-existent index; use a full chunk worth
            var strayFilePath = Path.Combine(worldDataDirectory.TerrainDirectory, ".DS_Store");
            File.WriteAllBytes(strayFilePath, new byte[TerrainTransferMeta.ChunkByteSize]);

            Assert.AreEqual(chunkTotalBeforeStrayFile, TerrainTransferMetaReader.Read(worldDataDirectory).TerrainChunkTotal);
        }

        [Test]
        public void ワールドseedはworld_jsonの値そのままメタに載る()
        {
            // クライアントはこのseedで分類段を再実行する。別の値を載せると別ワールドの海岸線が転送地形に貼られる
            // Clients re-run the classification stage with this seed; any other value paints another world's coastline onto the transferred terrain
            const int seed = 12345;
            var worldDataDirectory = _testScope.ProvisionGeneratedWorld(seed);

            var worldMeta = JsonConvert.DeserializeObject<WorldMetaJson>(File.ReadAllText(worldDataDirectory.WorldMetaFilePath));
            Assert.AreEqual(seed, worldMeta.Seed, "前提: 指定したseedがworld.jsonに記録されている");

            Assert.AreEqual(seed, TerrainTransferMetaReader.Read(worldDataDirectory).WorldSeed);
        }

        // キー欠損を0として読み進めると、探索無効ワールドの正当な0と区別が付かないまま別の場所の地形を配ることになる
        // Reading a missing key as 0 is indistinguishable from a search-less world's legitimate 0 and would ship another place's terrain
        [TestCase("terrainNoiseOriginX")]
        [TestCase("terrainNoiseOriginZ")]
        [TestCase("terrainSceneOriginX")]
        [TestCase("terrainSceneOriginZ")]
        [TestCase("generationMasterFingerprint")]
        public void generatedのworld_jsonに原点キーが欠けていたら0で補わず例外を投げる(string missingKey)
        {
            var worldDataDirectory = _testScope.ProvisionGeneratedWorld(12345);

            var worldMeta = JObject.Parse(File.ReadAllText(worldDataDirectory.WorldMetaFilePath));
            Assert.IsTrue(worldMeta.Remove(missingKey), $"前提: world.jsonに'{missingKey}'が書かれている");
            File.WriteAllText(worldDataDirectory.WorldMetaFilePath, worldMeta.ToString());

            // 0で読み進めるとクライアントは別の場所のノイズ窓で分類段を回し、転送地形に無関係な海岸線と草を貼る
            // Reading on with 0 would make clients classify another place's noise window, painting an unrelated coastline onto the transferred terrain
            var exception = Assert.Throws<InvalidOperationException>(() => TerrainTransferMetaReader.Read(worldDataDirectory));

            // 読み手はパケット応答経路で例外を握り潰されるため、ログ1行からどのworld.jsonをどうするか分かる必要がある
            // The caller sits on a packet path that swallows exceptions, so the single log line must say which world.json to act on
            Assert.That(exception.Message, Does.Contain(worldDataDirectory.WorldMetaFilePath));
        }

        // 旧バージョンのheightは摂動後の意味で書かれている。新クライアントが順適用すると摂動が二重に乗る
        // An older height file means post-perturbation; a new client applying it again would double the perturbation
        [Test]
        public void generatedのworld_jsonのgeneratorVersionが不一致なら例外を投げる()
        {
            var worldDataDirectory = _testScope.ProvisionGeneratedWorld(12345);

            var worldMeta = JObject.Parse(File.ReadAllText(worldDataDirectory.WorldMetaFilePath));
            worldMeta["generatorVersion"] = "1.0.0";
            File.WriteAllText(worldDataDirectory.WorldMetaFilePath, worldMeta.ToString());

            var exception = Assert.Throws<InvalidOperationException>(() => TerrainTransferMetaReader.Read(worldDataDirectory));

            // 読み手はパケット応答経路で例外を握り潰されるため、ログ1行からどのworld.jsonをどうするか分かる必要がある
            // The caller sits on a packet path that swallows exceptions, so the single log line must say which world.json to act on
            Assert.That(exception.Message, Does.Contain(worldDataDirectory.WorldMetaFilePath));
        }

        // templateは地形を生成せず原点という概念自体が無い。旧バージョンが書いたキー無しのworld.jsonも読めねばならない
        // Template worlds generate no terrain and have no origin concept, so a key-less world.json written by an older build must still read
        [Test]
        public void templateのworld_jsonは原点キーが無くても読める()
        {
            var worldDataDirectory = _testScope.ProvisionTemplateWorld(12345);

            var worldMeta = JObject.Parse(File.ReadAllText(worldDataDirectory.WorldMetaFilePath));
            foreach (var originKey in new[] { "terrainNoiseOriginX", "terrainNoiseOriginZ", "terrainSceneOriginX", "terrainSceneOriginZ" })
                worldMeta.Remove(originKey);
            File.WriteAllText(worldDataDirectory.WorldMetaFilePath, worldMeta.ToString());

            var meta = TerrainTransferMetaReader.Read(worldDataDirectory);

            Assert.AreEqual(WorldMapMode.Template, meta.MapMode);
            Assert.AreEqual(Vector2.zero, meta.Origins.NoiseOrigin);
            Assert.AreEqual(Vector2.zero, meta.Origins.SceneOrigin);
        }

        [Test]
        public void ワールドディレクトリを持たない構成のseedは0になる()
        {
            // world.jsonが無い＝seedという概念が存在しない構成。地形なしの合図はTerrainResolution=0が担う
            // No world.json means the concept of a seed does not exist here; TerrainResolution=0 remains the terrain-less signal
            Assert.AreEqual(0, TerrainTransferMeta.CreateWithoutWorldDirectory().WorldSeed);
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
        public void 論理ストリームのファイル列はタイル順にheightを並べる()
        {
            var worldDataDirectory = _testScope.CreateEmptyWorldDataDirectory();

            // この並びがチャンク境界の定義そのもの。取り違えても総バイト数は変わらないためここで固定する
            // This order defines the chunk boundaries; a swap keeps the byte total identical, so pin it here
            var expectedFilePaths = new[]
            {
                worldDataDirectory.TerrainHeightFilePath(0, 0),
                worldDataDirectory.TerrainHeightFilePath(1, 0),
                worldDataDirectory.TerrainHeightFilePath(0, 1),
                worldDataDirectory.TerrainHeightFilePath(1, 1),
            };

            Assert.AreEqual(expectedFilePaths, TerrainTransferMeta.EnumerateStreamFilePaths(worldDataDirectory, 4).ToArray());
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
