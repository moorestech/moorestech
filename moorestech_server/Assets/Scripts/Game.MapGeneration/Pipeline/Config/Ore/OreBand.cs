namespace Game.MapGeneration.Pipeline.Config
{
    // 鉱脈エントリ内の1つの距離バンド（スポーン地点中心の同心円リング）。
    // A single distance band (concentric ring around spawn) within an ore entry.
    public class OreBand
    {
        // -1（負値）は無限（最外周）。
        // -1 (negative) means infinite (outermost ring).
        public float outerRadiusMeters = -1f;
        public float density = 0.5f;
        public int maxObjectsPerCluster = 5;
        public float clusterRadius = 8f;
        public float minDistanceBetweenOres = 1.5f;
        public int placementRetries = 10;

        // リングプランナーへ渡す外半径列。バンドの並び順をそのまま保つ。
        // The outer-radius sequence handed to the ring planner, keeping band order.
        public static float[] OuterRadiiOf(OreBand[] bands)
        {
            var radii = new float[bands.Length];
            for (var i = 0; i < bands.Length; i++) radii[i] = bands[i].outerRadiusMeters;
            return radii;
        }
    }
}
