namespace Game.MapGeneration.Pipeline.Config
{
    // スポーン地点中心の同心円リングを1本表す帯の共通基底。鉱脈帯と散布帯が継承する。
    // Common base for one concentric band around the spawn point; vein bands and scatter bands derive from it.
    public abstract class SpawnDistanceBand
    {
        // -1（負値）は無限（最外周）。
        // -1 (negative) means infinite (outermost ring).
        public float outerRadiusMeters = -1f;

        // リング用外半径列（並び順保持）。
        // Outer radii for the ring planner (order preserved).
        internal static float[] OuterRadiiOf(SpawnDistanceBand[] bands)
        {
            var radii = new float[bands.Length];
            for (var i = 0; i < bands.Length; i++) radii[i] = bands[i].outerRadiusMeters;
            return radii;
        }
    }
}
