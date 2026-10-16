namespace Game.MapGeneration.Surface
{
    /// <summary>
    ///     UnityのTerrainDataが高さを格納する16bit格子。生成側の量子化が参照する
    ///     The 16-bit lattice TerrainData stores heights on; generation quantization refers to it
    /// </summary>
    public static class TerrainHeightStorage
    {
        // TerrainDataは正規化高さ0..1を0..32766の整数段で保持する
        // TerrainData keeps normalized height 0..1 as integer steps 0..32766
        public const int Steps = 32766;

        // 鉱脈パッドを採掘底面の整数より下げる余裕。生成側はこの分を引いた格子段にパッドを置く
        // Clearance keeping a vein pad below the integer mining bottom; generation stores the pad this far below
        public const double MiningBottomClearanceMeters = 0.001d;
    }
}
