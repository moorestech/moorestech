namespace MapGenerator.Pipeline.Config
{
    /// <summary>
    /// 全 Config クラスで共有するノイズタイプ enum。
    /// テクスチャ・Detail・Object の密度変調で使う。
    /// </summary>
    public enum MapNoiseType { None, WormFBM, Worley, Simple, FBM }
}
