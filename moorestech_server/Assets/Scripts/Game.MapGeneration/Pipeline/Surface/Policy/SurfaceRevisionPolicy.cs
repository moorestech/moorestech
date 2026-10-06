using Game.MapGeneration.Facade.Surface;
using Game.MapGeneration.Pipeline.Config;

namespace Game.MapGeneration.Pipeline.Surface
{
    public abstract class SurfaceRevisionPolicy
    {
        public abstract IMapGenerator Generator { get; }
        internal abstract string VisualCacheVersionSuffix { get; }
        public abstract SurfaceHeightPolicy CreateHeightPolicy(TerrainGenerationConfig config);

        internal sealed class Legacy : SurfaceRevisionPolicy
        {
            public override IMapGenerator Generator { get; } = new VanillaGenerator();
            internal override string VisualCacheVersionSuffix => string.Empty;

            public override SurfaceHeightPolicy CreateHeightPolicy(TerrainGenerationConfig config)
            {
                return new LegacySurfaceHeightPolicy();
            }
        }

        internal sealed class Grounded : SurfaceRevisionPolicy
        {
            public override IMapGenerator Generator { get; } = new GroundedVanillaGenerator();
            internal override string VisualCacheVersionSuffix => "|owned-display-1";

            public override SurfaceHeightPolicy CreateHeightPolicy(TerrainGenerationConfig config)
            {
                return new GroundedSurfaceHeightPolicy(config, SurfaceEnvelope.GeneratedV5);
            }
        }
    }
}
