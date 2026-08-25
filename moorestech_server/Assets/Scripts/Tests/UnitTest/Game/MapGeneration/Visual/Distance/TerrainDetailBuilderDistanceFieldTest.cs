using NUnit.Framework;

namespace Tests.UnitTest.Game.MapGeneration.Visual.Distance
{
    /// <summary>
    ///     DetailへSDF距離マップが実際に届いているかと、木と岩が別々の距離場に入っているかを検証する。
    ///     距離場が抜けても例外は出ず、木の根元に草が生え続ける形でしか現れない
    ///     Verifies the SDF distance maps really reach the detail path and that trees and rocks land in separate fields.
    ///     A missing field throws nothing: it only shows as grass growing at every tree trunk
    /// </summary>
    public class TerrainDetailBuilderDistanceFieldTest
    {
        private const int MaxDensity = DistanceFieldTestScene.MaxDensity;
        private const int DetailResolution = DistanceFieldTestScene.DetailResolution;

        // 木から94m離れた画素。帯(10m以遠・200m未満)の内側にあり、距離場が本当に届いていれば通る
        // A pixel 94m from the tree: inside the 10m-to-200m band, so it passes only when the field really arrived
        private const int InBandPixel = 2;

        [SetUp]
        public void SetUp()
        {
            DistanceFieldTestScene.LoadMasterData();
        }

        [Test]
        public void RejectsThePixelStandingOnATreeInsideTheTile()
        {
            // 距離場が届いていなければ木の真下も帯の内側も同じ値になる。両方を見て初めて供給の証拠になる
            // Without a field the trunk pixel and the in-band pixel would agree; only both together prove the supply
            var withTree = DistanceFieldTestScene.Build(
                DistanceFieldTestScene.TreeDistanceSections(),
                DistanceFieldTestScene.CreateMapObject(DistanceFieldTestScene.TreeGuid, localX: 0f, localZ: 0f));

            Assert.That(withTree[0][0, 0], Is.EqualTo(0), "木の真下は距離フィルタで落ちる");
            Assert.That(withTree[0][InBandPixel, InBandPixel], Is.EqualTo(MaxDensity), "帯の内側の画素は残る");
        }

        [Test]
        public void RejectsEveryPixelOnATileWhoseHaloHoldsNoTree()
        {
            // 木がゼロなら真の最寄り距離は打ち切り値を超える。距離場をnullで休ませると隣タイルとの間で草の有無が反転する
            // With no trees the true nearest distance exceeds the cutoff; idling the field on null flips grass coverage across the seam
            var treeFreeTile = DistanceFieldTestScene.Build(DistanceFieldTestScene.TreeDistanceSections());
            var withTree = DistanceFieldTestScene.Build(
                DistanceFieldTestScene.TreeDistanceSections(),
                DistanceFieldTestScene.CreateMapObject(DistanceFieldTestScene.TreeGuid, localX: 0f, localZ: 0f));

            for (var z = 0; z < DetailResolution; z++)
            for (var x = 0; x < DetailResolution; x++)
                Assert.That(treeFreeTile[0][z, x], Is.EqualTo(0), $"木ゼロのタイルは全画素が打ち切り距離で落ちる z={z} x={x}");

            Assert.That(withTree[0][InBandPixel, InBandPixel], Is.EqualTo(MaxDensity), "木が1本入れば同じ画素が通る");
        }

        [Test]
        public void FeedsTreesAndRocksIntoSeparateDistanceFields()
        {
            // terrainSurroundEffectTypeでの振り分けを外すと岩が木の距離場に混ざり、岩の周りだけ草が消える
            // Losing the terrainSurroundEffectType split mixes rocks into the tree field and clears the grass around every rock
            var treeFilteredWithRock = DistanceFieldTestScene.Build(
                DistanceFieldTestScene.TreeDistanceSections(),
                DistanceFieldTestScene.CreateMapObject(DistanceFieldTestScene.StoneGuid, localX: 0f, localZ: 0f));
            var objectFilteredWithRock = DistanceFieldTestScene.Build(
                DistanceFieldTestScene.ObjectDistanceSections(),
                DistanceFieldTestScene.CreateMapObject(DistanceFieldTestScene.StoneGuid, localX: 0f, localZ: 0f));
            var objectFilteredWithTree = DistanceFieldTestScene.Build(
                DistanceFieldTestScene.ObjectDistanceSections(),
                DistanceFieldTestScene.CreateMapObject(DistanceFieldTestScene.TreeGuid, localX: 0f, localZ: 0f));

            Assert.That(treeFilteredWithRock[0][InBandPixel, InBandPixel], Is.EqualTo(0), "岩は木の距離場に入らないので木ゼロ扱いになる");
            Assert.That(objectFilteredWithRock[0][0, 0], Is.EqualTo(0), "岩の真下はオブジェクトの距離場で落ちる");
            Assert.That(objectFilteredWithRock[0][InBandPixel, InBandPixel], Is.EqualTo(MaxDensity), "岩はオブジェクトの距離場に入る");
            Assert.That(objectFilteredWithTree[0][InBandPixel, InBandPixel], Is.EqualTo(0), "木はオブジェクトの距離場に入らない");
        }

        // 本番の解像度差ではalphamap距離場の北西半分しかdetailから読めず、南東の木・岩と10m境界が消える
        // At production's resolution gap, an alphamap-sized field exposes only its north-west half to detail, losing south-east objects and the 10m boundary
        [Test]
        public void ProductionResolutionKeepsTreeAndObjectDistanceBoundariesAtSouthEastTest()
        {
            const int southEastPixel = DistanceFieldTestScene.ProductionDetailResolution - 1;
            var rejectedPixelDistance = (int)(
                DistanceFieldTestScene.RejectedWithin / DistanceFieldTestScene.TileSize * southEastPixel);
            var lastRejectedPixel = southEastPixel - rejectedPixelDistance;
            var firstAcceptedPixel = lastRejectedPixel - 1;
            var maps = DistanceFieldTestScene.BuildAtProductionResolution(
                DistanceFieldTestScene.TreeAndObjectDistanceSections(),
                DistanceFieldTestScene.CreateMapObject(DistanceFieldTestScene.TreeGuid, DistanceFieldTestScene.TileSize, DistanceFieldTestScene.TileSize),
                DistanceFieldTestScene.CreateMapObject(DistanceFieldTestScene.StoneGuid, DistanceFieldTestScene.TileSize, DistanceFieldTestScene.TileSize));

            Assert.That(maps.Count, Is.EqualTo(2));
            Assert.That(maps[0].GetLength(0), Is.EqualTo(DistanceFieldTestScene.ProductionDetailResolution));
            Assert.That(maps[0].GetLength(1), Is.EqualTo(DistanceFieldTestScene.ProductionDetailResolution));
            AssertSouthEastBoundary(maps[0], "木");
            AssertSouthEastBoundary(maps[1], "岩");

            #region Internal

            void AssertSouthEastBoundary(int[,] map, string kind)
            {
                Assert.That(map[southEastPixel, southEastPixel], Is.EqualTo(0), $"{kind}の真下は落ちる");
                Assert.That(map[lastRejectedPixel, southEastPixel], Is.EqualTo(0), $"{kind}から10m未満は落ちる");
                Assert.That(map[firstAcceptedPixel, southEastPixel], Is.EqualTo(MaxDensity), $"{kind}から10mを越えた最初の画素は残る");
            }

            #endregion
        }
    }
}
