using UnityEngine;

namespace MapGenerator.Pipeline.Config
{
    /// <summary>
    /// 鉱石エントリ内の1つの距離バンド（スポーン地点を中心とした同心円リング）。
    /// outerRadiusMeters がこのバンドの外周半径。-1 は無限（最外周）。
    /// </summary>
    [System.Serializable]
    public class OreBand
    {
        // -1（負値はすべて）= 無限（最外周）。-1以外の負値はOrePlacementGeneratorが警告する。
        [Label("スポーン地点からの外周半径(m, -1=無限)")]
        public float outerRadiusMeters = -1f;

        [Label("密度")]
        [Range(0.0f, 5f)]
        public float density = 0.5f;

        [Label("クラスターあたりの最大オブジェクト数")]
        [Range(1, 20)]
        public int maxObjectsPerCluster = 5;

        [Label("クラスター半径(m)")]
        [Range(1f, 50f)]
        public float clusterRadius = 8f;

        [Label("鉱石同士の最小距離(m)")]
        [Range(0f, 20f)]
        public float minDistanceBetweenOres = 1.5f;

        [Label("配置リトライ回数")]
        [Range(1, 30)]
        public int placementRetries = 10;
    }
}
