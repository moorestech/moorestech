namespace MapGenerator.Pipeline.Config
{
    /// <summary>
    /// ノイズレイヤー合成の演算子。DetailNoiseStack や clusterNoise の合成方法を決定する。
    /// </summary>
    public enum NoiseOp { Add, Subtract, Multiply, Overlay, Min, Max }
}
