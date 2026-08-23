using Game.MapGeneration.Pipeline.Config;

namespace Game.MapGeneration.Pipeline.Visual.Splat
{
    /// <summary>
    ///     台地デバッグオーバーレイを走らせるかどうかの唯一の判定。デバッグ列を確保する側と塗る側が別々の条件を持つと、
    ///     誰も塗らない列のTerrainLayerをAddressablesから読むか、逆に列0本のまま塗って台地を全消しするかのどちらかになる
    ///     The single verdict on whether the plateau debug overlay runs; letting the column reservation and the painting
    ///     carry separate conditions either loads TerrainLayers nobody paints or paints with zero columns and wipes the plateaus
    /// </summary>
    public static class PlateauDebugOverlayGate
    {
        public static bool IsEnabled(TerrainGenerationConfig config)
        {
            return config.alpineEnabled && config.alpine.enablePlateau && config.alpine.debugPlateauOverlay;
        }
    }
}
