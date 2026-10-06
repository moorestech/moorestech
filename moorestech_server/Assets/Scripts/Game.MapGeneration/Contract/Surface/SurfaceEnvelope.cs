namespace Game.MapGeneration.Surface
{
    // 描画海面と鉱脈整地が共有する生成版固有の契約
    // Revision-specific contract shared by the rendered sea and vein grading
    public readonly struct SurfaceEnvelope
    {
        public static readonly SurfaceEnvelope GeneratedV5 = new SurfaceEnvelope(4.3f, 0.5f, 0.1f, 2f, 2f);

        public readonly float SeaY;
        public readonly float MaximumWaveRise;
        public readonly float LandClearance;
        public readonly float CoreHalfSize;
        public readonly float BlendWidth;

        private SurfaceEnvelope(float seaY, float maximumWaveRise, float landClearance, float coreHalfSize, float blendWidth)
        {
            SeaY = seaY;
            MaximumWaveRise = maximumWaveRise;
            LandClearance = landClearance;
            CoreHalfSize = coreHalfSize;
            BlendWidth = blendWidth;
        }
    }
}
