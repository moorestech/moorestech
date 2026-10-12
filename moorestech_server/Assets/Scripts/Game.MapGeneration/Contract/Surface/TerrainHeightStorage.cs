namespace Game.MapGeneration.Surface
{
    /// <summary>
    ///     UnityのTerrainDataが高さを格納する16bit格子。生成側の量子化と表示・設置側の許容誤差が同じ格子を参照する
    ///     The 16-bit lattice TerrainData stores heights on; generation quantization and presentation/placement tolerances share it
    /// </summary>
    public static class TerrainHeightStorage
    {
        // TerrainDataは正規化高さ0..1を0..32766の整数段で保持する
        // TerrainData keeps normalized height 0..1 as integer steps 0..32766
        public const int Steps = 32766;

        // 鉱脈パッドを採掘底面の整数より下げる余裕。生成側はこの分を引いた格子段にパッドを置き、設置側はこの分も整数へ引き上げる
        // Clearance keeping a vein pad below the integer mining bottom; generation stores the pad this far below and placement lifts it back
        public const double MiningBottomClearanceMeters = 0.001d;

        // 地形の高さ範囲1段ぶんのメートル
        // Meters spanned by one storage step for the given terrain height range
        public static float StepMeters(float terrainHeight)
        {
            return terrainHeight / Steps;
        }
    }
}
