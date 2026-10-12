using System;
using Game.MapGeneration.Transfer;
using NUnit.Framework;

namespace Tests.UnitTest.Game.MapGeneration.Transfer
{
    [Category("CiShardServerMap3")]
    public class TerrainTransferTileCoordinatesTest
    {
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
        public void タイル数が0以下ならチャンク総数0を返さず例外を投げる()
        {
            // 0は完全平方数なので格子ガードを素通りする。無言でチャンク0本のワイヤ値を返させない
            // Zero is a perfect square and slips past the grid guard; never let it silently yield a zero-chunk wire value
            Assert.Throws<InvalidOperationException>(() => TerrainTransferMeta.EnumerateTileCoordinates(0));
            Assert.Throws<InvalidOperationException>(() => TerrainTransferMeta.EnumerateTileCoordinates(-1));
        }

    }
}
