namespace Game.MapGeneration.Surface
{
    // 描画海面と鉱脈整地が共有する生成版固有の契約
    // Revision-specific contract shared by the rendered sea and vein grading
    public readonly struct SurfaceEnvelope
    {
        // BlendWidth 2mは本番格子間隔(約3.9m)より狭い。支持頂点の外の頂点はcoreから格子間隔以上離れるため、本番ではskirtが頂点へ届かない
        // BlendWidth 2m is narrower than the production lattice spacing (~3.9m); vertices past the support sit a full spacing from the core, so production skirts reach no vertex
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
