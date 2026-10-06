using Game.MapGeneration.Facade.Surface;
using Game.MapGeneration.Pipeline.Config;

namespace Game.MapGeneration.Pipeline.Surface
{
    public abstract class SurfaceRevisionPolicy
    {
        public abstract IMapGenerator Generator { get; }
        public abstract SurfaceHeightPolicy CreateHeightPolicy(TerrainGenerationConfig config);

        internal sealed class Legacy : SurfaceRevisionPolicy
        {
            public override IMapGenerator Generator { get; } = new VanillaGenerator();

            public override SurfaceHeightPolicy CreateHeightPolicy(TerrainGenerationConfig config)
            {
                return new LegacySurfaceHeightPolicy();
            }
        }

        internal sealed class Grounded : SurfaceRevisionPolicy
        {
            public override IMapGenerator Generator { get; } = new GroundedVanillaGenerator();

            public override SurfaceHeightPolicy CreateHeightPolicy(TerrainGenerationConfig config)
            {
                return new GroundedSurfaceHeightPolicy(config, SurfaceEnvelope.GeneratedV5);
            }
        }
    }
}
