using Game.MapGeneration.Surface;

namespace Game.MapGeneration.Pipeline.Surface
{
    // 地表revisionごとの生成器・表示契約・鉱脈整地の有無を1か所で束ねる
    // Bundles each surface revision's generator, presentation contract and vein grading in one place
    internal abstract class SurfaceRevisionPolicy
    {
        public abstract IMapGenerator Generator { get; }
        public abstract TerrainSurfacePresentation Presentation { get; }

        // 台帳padが鉱脈から決まる版か。真なら保存鉱脈と再生成鉱脈の一致が台帳を進める条件になる
        // Whether ledger pads derive from veins; when true the saved and regenerated vein sets must match before the ledger advances
        internal abstract bool GradesTerrainAroundVeins { get; }

        internal sealed class Legacy : SurfaceRevisionPolicy
        {
            public override IMapGenerator Generator { get; } = new LegacyVanillaGenerator();
            public override TerrainSurfacePresentation Presentation { get; } = new TerrainSurfacePresentation.Legacy();
            internal override bool GradesTerrainAroundVeins => false;
        }

        internal sealed class Grounded : SurfaceRevisionPolicy
        {
            public override IMapGenerator Generator { get; }
            public override TerrainSurfacePresentation Presentation { get; }
            internal override bool GradesTerrainAroundVeins => true;

            public Grounded()
            {
                var envelope = SurfaceEnvelope.GeneratedV5;
                Generator = new GroundedVanillaGenerator(envelope);
                Presentation = new TerrainSurfacePresentation.Grounded(envelope);
            }
        }
    }
}
