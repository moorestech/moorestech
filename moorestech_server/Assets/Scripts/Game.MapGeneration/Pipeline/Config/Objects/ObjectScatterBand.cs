namespace Game.MapGeneration.Pipeline.Config
{
    // 独立散布の1スポーン距離バンド（同心円リング）。
    // A single spawn-distance band (concentric ring around spawn) for object scatter.
    public class ObjectScatterBand
    {
        // -1（負値）は無限（最外周）。
        // -1 (negative) means infinite (outermost ring).
        public float outerRadiusMeters = -1f;

        // 非クラスタ散布の1haあたり密度。
        // Per-hectare density for non-cluster scatter.
        public float density = 1f;

        // クラスタモード時のクラスタ数上限。
        // Cluster cap for cluster mode.
        public int clusterCount = 8;

        // リング用外半径列（並び順保持）。
        // Outer radii for the ring planner (order preserved).
        internal static float[] OuterRadiiOf(ObjectScatterBand[] bands)
        {
            var radii = new float[bands.Length];
            for (var i = 0; i < bands.Length; i++) radii[i] = bands[i].outerRadiusMeters;
            return radii;
        }
    }
}
