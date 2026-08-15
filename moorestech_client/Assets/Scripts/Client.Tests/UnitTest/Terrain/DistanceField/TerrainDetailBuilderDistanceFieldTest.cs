using System.Collections.Generic;
using Client.Game.InGame.Environment.Terrain.Build;
using Client.Game.InGame.Environment.Terrain.Visual.Detail;
using Client.Game.InGame.Environment.Terrain.Visual.Source;
using Client.Game.InGame.Environment.Terrain.Visual.Splat;
using Game.MapGeneration.Pipeline.Biomes;
using Game.MapGeneration.Pipeline.Config;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse.MapData;
using Tests.Module.TestMod;
using UnityEngine;

namespace Client.Tests.UnitTest.Terrain.DistanceField
{
    /// <summary>
    ///     DetailへSDF距離マップが実際に届いているかと、その入力がタイル境界の外まで広がっているかを検証する。
    ///     距離場が抜けても例外は出ず、木の根元に草が生え続ける・境界に沿って草の密度が段差になる形でしか現れない
    ///     Verifies the SDF distance maps really reach the detail path and that their input spans past the tile boundary.
    ///     A missing field throws nothing: it only shows as grass growing at tree trunks and a density step along the seam
    /// </summary>
    public class TerrainDetailBuilderDistanceFieldTest
    {
        private const int Resolution = 5;
        private const int DetailResolution = Resolution - 1;
        private const int MaxDensity = 8;
        private const float TileSize = 100f;

        // 距離フィルタは10m以遠だけを通す。上限200mはタイル対角(141m)より広く、打ち切りで誤判定しない
        // The distance filter admits 10m and beyond; the 200m ceiling clears the tile diagonal (141m) so the cutoff never decides
        private const float RejectedWithin = 10f;
        private const float SearchCeiling = 200f;

        private const string TreeGuid = "00000000-0000-1111-0000-000000000001";
        private const string StoneGuid = "00000000-0000-2222-0000-000000000001";

        // 原点から離れた負のZを持つタイル。ローカル化の取り違えを距離値の桁で露出させる
        // A tile away from the origin with a negative Z, so a rebasing mistake shows up as an order-of-magnitude distance
        private static readonly Vector3 TilePosition = new(100f, 0f, -200f);

        private static readonly BiomeType[] BiomeTypes = { BiomeType.Grassland };

        [SetUp]
        public void SetUp()
        {
            // MapObjectPointSplitterがsoundEffectTypeを引くためMasterHolderが要る
            // MapObjectPointSplitter reads soundEffectType, so MasterHolder must be loaded
            new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [Test]
        public void RejectsThePixelStandingOnATreeInsideTheTile()
        {
            // 距離場がnullのままなら距離フィルタは有効でも休む。木の真下の画素が落ちることが供給の直接の証拠
            // A null field idles the filter even while enabled; the pixel under the trunk dropping is direct evidence of supply
            var withTree = Build(TreeDistanceSections(), CreateMapObject(TreeGuid, localX: 0f, localZ: 0f));
            var withoutObjects = Build(TreeDistanceSections());

            Assert.That(withTree[0][0, 0], Is.EqualTo(0), "木の真下は距離フィルタで落ちる");
            Assert.That(withoutObjects[0][0, 0], Is.EqualTo(MaxDensity), "距離場が無ければフィルタは休む");
        }

        [Test]
        public void SeesTheNeighbouringTilesTreeThroughTheHalo()
        {
            // haloが無ければタイル外の木は入力から丸ごと消え、境界画素だけが「近くに木が無い」と誤判定する
            // Without the halo the out-of-tile tree vanishes from the input and the edge pixel alone misreads as tree-free
            var withNeighbourTree = Build(TreeDistanceSections(), CreateMapObject(TreeGuid, localX: -2f, localZ: 0f));
            var withoutObjects = Build(TreeDistanceSections());

            Assert.That(withNeighbourTree[0][0, 0], Is.EqualTo(0), "境界の外2mの木は境界画素へ届く");
            Assert.That(withoutObjects[0][0, 0], Is.EqualTo(MaxDensity), "同じ画素はhalo無し相当では素通しになる");

            // 内側の画素は距離が探索半径の内側で通る。ここまで落ちるなら距離場ではなく別の要因で落ちている
            // Interior pixels stay inside the search radius and pass; their dropping would mean something other than the field rejected them
            Assert.That(withNeighbourTree[0][2, 2], Is.EqualTo(MaxDensity), "遠い画素は影響を受けない");
        }

        [Test]
        public void KeepsTheHaloWindowWiderThanTheSearchRadiusOnEveryEdge()
        {
            // 4辺のうち1辺でも広げ忘れると、その辺に沿った1画素列だけが素通しの帯になる
            // Forgetting one of the four edges leaves a single pixel column along it passing straight through
            var eastEdge = Build(TreeDistanceSections(), CreateMapObject(TreeGuid, localX: TileSize + 2f, localZ: TileSize));
            var northEdge = Build(TreeDistanceSections(), CreateMapObject(TreeGuid, localX: 0f, localZ: TileSize + 2f));

            Assert.That(eastEdge[0][DetailResolution - 1, DetailResolution - 1], Is.EqualTo(0), "東の外の木が届く");
            Assert.That(northEdge[0][DetailResolution - 1, 0], Is.EqualTo(0), "北の外の木が届く");
        }

        [Test]
        public void FeedsTreesAndRocksIntoSeparateDistanceFields()
        {
            // soundEffectTypeでの振り分けを外すと岩が木の距離場に混ざり、岩の周りだけ草が消える
            // Losing the soundEffectType split mixes rocks into the tree field and clears the grass around every rock
            var treeFilteredWithRock = Build(TreeDistanceSections(), CreateMapObject(StoneGuid, localX: 0f, localZ: 0f));
            var objectFilteredWithRock = Build(ObjectDistanceSections(), CreateMapObject(StoneGuid, localX: 0f, localZ: 0f));
            var objectFilteredWithTree = Build(ObjectDistanceSections(), CreateMapObject(TreeGuid, localX: 0f, localZ: 0f));

            Assert.That(treeFilteredWithRock[0][0, 0], Is.EqualTo(MaxDensity), "岩は木の距離場に入らない");
            Assert.That(objectFilteredWithRock[0][0, 0], Is.EqualTo(0), "岩はオブジェクトの距離場に入る");
            Assert.That(objectFilteredWithTree[0][0, 0], Is.EqualTo(MaxDensity), "木はオブジェクトの距離場に入らない");
        }

        private static List<int[,]> Build(BiomeVisualSections visualSections, params MapObjectLayoutMessagePack[] mapObjects)
        {
            var config = CreateConfig();
            var heights = new float[Resolution, Resolution];

            return TerrainDetailBuilder.Build(
                config, BiomeTypes, visualSections, heights, heights, CreateWinnerMasks(), null, null,
                mapObjects, TilePosition);
        }

        private static BiomeVisualSections TreeDistanceSections()
        {
            var entry = DetailTestConfigBuilder.CreateEntry(1f, MaxDensity);
            entry.treeDistanceFilter = CreateDistanceFilter();
            return CreateSections(entry);
        }

        private static BiomeVisualSections ObjectDistanceSections()
        {
            var entry = DetailTestConfigBuilder.CreateEntry(1f, MaxDensity);
            entry.objectDistanceFilter = CreateDistanceFilter();
            return CreateSections(entry);
        }

        private static DetailFilter CreateDistanceFilter()
        {
            var filter = DetailTestConfigBuilder.CreateDisabledFilter();
            filter.enabled = true;
            filter.range = new Vector2(RejectedWithin, SearchCeiling);
            filter.smoothness = Vector2.zero;
            return filter;
        }

        private static BiomeVisualSections CreateSections(DetailEntry entry)
        {
            var detailConfig = new BiomeDetailConfig
            {
                entries = new[] { entry }, filterRejectThreshold = 0.01f, borderMargin = 0f,
            };

            return new BiomeVisualSections(
                new string[BiomeTypes.Length], new BiomeTextureConfig[BiomeTypes.Length], new[] { detailConfig });
        }

        private static TerrainGenerationConfig CreateConfig()
        {
            return new TerrainGenerationConfig
            {
                overrideResolution = Resolution, seed = 4321,
                terrainWidth = TileSize, terrainLength = TileSize, terrainHeight = 50f,
            };
        }

        private static bool[][,] CreateWinnerMasks()
        {
            var mask = new bool[Resolution, Resolution];
            for (var z = 0; z < Resolution; z++)
            for (var x = 0; x < Resolution; x++)
                mask[z, x] = true;

            return new[] { mask };
        }

        // 引数はタイルローカル。シーン絶対座標へ戻して渡し、切り出しのローカル化まで通しで検証する
        // The arguments are tile-local and get pushed back to scene-absolute, exercising the slicer's rebasing end to end
        private static MapObjectLayoutMessagePack CreateMapObject(string mapObjectGuid, float localX, float localZ)
        {
            return new MapObjectLayoutMessagePack(
                1, mapObjectGuid, TilePosition.x + localX, 0f, TilePosition.z + localZ,
                1f, 1f, 1f, -1, 0f, 0f);
        }
    }
}
