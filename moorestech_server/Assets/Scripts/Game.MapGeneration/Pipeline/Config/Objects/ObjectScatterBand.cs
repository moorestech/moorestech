namespace Game.MapGeneration.Pipeline.Config
{
    // 独立散布エントリ内の1つのスポーン距離バンド（スポーン地点中心の同心円リング）。
    // A single spawn-distance band (concentric ring around spawn) within an object scatter entry.
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

        // リングプランナーへ渡す外半径列。バンドの並び順をそのまま保つ。
        // The outer-radius sequence handed to the ring planner, keeping band order.
        public static float[] OuterRadiiOf(ObjectScatterBand[] bands)
        {
            var radii = new float[bands.Length];
            for (var i = 0; i < bands.Length; i++) radii[i] = bands[i].outerRadiusMeters;
            return radii;
        }
    }
}
