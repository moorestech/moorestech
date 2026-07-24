namespace Game.MapGeneration.Pipeline.Config
{
    // フィルタ・クラスタリングに使うノイズパラメータ（texture ソースはスキーマ化で削除）。
    // Noise parameters for filters/clustering (texture source removed by schema migration).
    public struct PlacementNoise
    {
        public MapNoiseType noiseType;
        public float frequency;
        public float amplitude;
        public float offset;
        public float balance;
    }
}
