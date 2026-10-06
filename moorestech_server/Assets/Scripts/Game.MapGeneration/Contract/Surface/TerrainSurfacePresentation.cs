namespace Game.MapGeneration.Surface
{
    // 表示側へ生成内部の設定を渡さず契約だけを公開する
    // Expose the presentation contract without leaking internal generation configuration
    public abstract class TerrainSurfacePresentation
    {
        private TerrainSurfacePresentation() { }

        public sealed class Legacy : TerrainSurfacePresentation { }

        public sealed class Grounded : TerrainSurfacePresentation
        {
            public readonly SurfaceEnvelope Envelope;

            public Grounded(SurfaceEnvelope envelope)
            {
                Envelope = envelope;
            }
        }
    }
}
