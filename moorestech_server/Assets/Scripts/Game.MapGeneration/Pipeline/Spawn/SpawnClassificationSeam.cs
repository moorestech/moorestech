using Game.MapGeneration.Pipeline.Biomes;
using Game.MapGeneration.Pipeline.Config;

namespace Game.MapGeneration.Pipeline.Spawn
{
    // 5b シーム: SpawnRegionFinder が要求する分類結果ウィンドウ（本番一致 final 検証用）。
    // 5b seam: the classification-result window SpawnRegionFinder consumes for final verification.
    public sealed class ClassificationWindow
    {
        public int Resolution;
        public float PitchX;
        public float PitchZ;
        public float OriginX;
        public float OriginZ;
        public int[] WinnerBiomeIndex;
        public float[] LandMask;
        public float[] BeachFactor;
    }

    // 5b シーム: TerrainGenerator のステージ分割で実装される分類関数のプレースホルダ。
    // 5a では SpawnRegionFinder の配線のみ確保し、実装は 5b の ClassificationStage が担う。
    // 5b seam: placeholder for the classification functions implemented by TerrainGenerator's
    // stage split. 5a only wires SpawnRegionFinder; 5b's ClassificationStage supplies the bodies.
    public static class SpawnClassificationSeam
    {
        public static CoarseBiomeGrid ClassifyRawGrid(
            TerrainGenerationConfig config, BiomeType[] biomeTypes,
            float centerX, float centerZ, float extent, float cellSize)
        {
            throw new System.NotImplementedException("5b: wire to ClassificationStage.ClassifyRawGrid");
        }

        public static ClassificationWindow RunClassificationDetailed(
            TerrainGenerationConfig config, BiomeType[] biomeTypes,
            float centerX, float centerZ, float windowSize)
        {
            throw new System.NotImplementedException("5b: wire to ClassificationStage.RunClassificationDetailed");
        }
    }
}
