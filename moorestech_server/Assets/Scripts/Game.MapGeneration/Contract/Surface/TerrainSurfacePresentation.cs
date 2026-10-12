namespace Game.MapGeneration.Surface
{
    // 表示側へ生成内部の設定を渡さず契約だけを公開する。消費側はvisitorで受け、版を足すと全実装がコンパイルエラーになる
    // Expose the presentation contract without leaking internal generation configuration; consumers take it through a visitor, so a new revision breaks every implementation at compile time
    public abstract class TerrainSurfacePresentation
    {
        private TerrainSurfacePresentation() { }

        public abstract void Accept(ITerrainSurfacePresentationVisitor visitor);

        public sealed class Legacy : TerrainSurfacePresentation
        {
            public override void Accept(ITerrainSurfacePresentationVisitor visitor)
            {
                visitor.VisitLegacy();
            }
        }

        public sealed class Grounded : TerrainSurfacePresentation
        {
            public readonly SurfaceEnvelope Envelope;

            public Grounded(SurfaceEnvelope envelope)
            {
                Envelope = envelope;
            }

            public override void Accept(ITerrainSurfacePresentationVisitor visitor)
            {
                visitor.VisitGrounded(Envelope);
            }
        }
    }
}
