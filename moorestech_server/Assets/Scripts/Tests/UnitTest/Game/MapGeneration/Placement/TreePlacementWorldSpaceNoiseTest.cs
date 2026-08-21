using System.Collections.Generic;
using System.Text;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Generators;
using Game.MapGeneration.Pipeline.Tiling;
using NUnit.Framework;

namespace Tests.UnitTest.Game.MapGeneration
{
    // 樹木配置の密度・フィルタノイズがタイル原点ではなくワールド座標で引かれることを固定する。
    // 乱数種と候補点列を完全に揃えたまま窓原点だけをずらすので、結果が動けばノイズが原点を読んでいる証拠になる。
    // Pins that tree placement's density and filter noise are drawn in world space rather than from the tile origin.
    // The seed and candidate series are held identical and only the window origin moves, so any change proves the noise reads that origin.
    public class TreePlacementWorldSpaceNoiseTest
    {
        private const int Resolution = 33;
        private const float TerrainSize = 500f;
        private const int PlacementSeed = 1;
        private const int NoiseSeed = 11;

        [Test]
        public void 乱数種が同じでも窓原点が違えば別の木が生える()
        {
            var atOrigin = Place(0f, 0);
            var oneTileRight = Place(TerrainSize, 1);

            Assert.IsNotEmpty(atOrigin, "フィクスチャが1本も木を置いていない");
            Assert.AreNotEqual(Describe(atOrigin), Describe(oneTileRight),
                "窓原点をタイル1枚ぶんずらしても同じ木が並ぶ＝ノイズがタイルローカル座標で引かれている");
        }

        // 完全な平地・全面マスクの1タイルへ1プロトタイプだけを配置する。地形が同一なので差の出所はノイズ座標だけ。
        // Places one prototype on a perfectly flat, fully masked tile; with identical terrain the only possible source of a difference is the noise coordinate.
        private static List<PlacementEntry> Place(float worldOffsetX, int tileIndexX)
        {
            var dims = new TerrainDimensions(
                TerrainSize, TerrainSize, 100f, worldOffsetX, 0f, Resolution, 0f, 0f, 1, 0f, 0f,
                tileIndexX, 0, 2, 1);

            var mask = new bool[Resolution, Resolution];
            for (int z = 0; z < Resolution; z++)
                for (int x = 0; x < Resolution; x++)
                    mask[z, x] = true;

            var treeConfig = new TreePlacementConfig
            {
                prototypes = new[]
                {
                    new TreePrototypeEntry
                    {
                        mapObjectGuids = new[] { "11111111-0000-0000-0000-000000000001" },
                        clusterNoiseThreshold = 0.3f,
                    },
                },
            };

            return TreePlacementGenerator.GenerateForBiome(
                mask, new float[Resolution * Resolution], dims, treeConfig,
                new System.Random(PlacementSeed), NoiseSeed, new PlacementHaloChannel(), 0f);
        }

        private static string Describe(List<PlacementEntry> placements)
        {
            var text = new StringBuilder();
            foreach (var placement in placements)
                text.Append(placement.WorldPosition.x).Append(',').Append(placement.WorldPosition.z).Append(';');
            return text.ToString();
        }
    }
}
