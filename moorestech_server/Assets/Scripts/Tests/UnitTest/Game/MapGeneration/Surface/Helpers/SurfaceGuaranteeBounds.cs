using Game.MapGeneration.Surface;

namespace Tests.UnitTest.Game.MapGeneration.Surface
{
    // 本番の版固有定数から組み立てた保証の許容値
    // Guarantee tolerances assembled from the production revision constants
    public static class SurfaceGuaranteeBounds
    {
        private const float FloatSlack = 0.00005f;

        public static float LandMinimum => LandMinimumOf(SurfaceEnvelope.GeneratedV5);
        public static float SeaY => SurfaceEnvelope.GeneratedV5.SeaY;
        public static float CoreFlatTolerance => FloatSlack;
        public static float RangeGapTolerance => (float)TerrainHeightStorage.MiningBottomClearanceMeters + FloatSlack;

        private static float LandMinimumOf(SurfaceEnvelope envelope)
        {
            return (float)((double)envelope.SeaY + envelope.MaximumWaveRise + envelope.LandClearance);
        }
    }
}
