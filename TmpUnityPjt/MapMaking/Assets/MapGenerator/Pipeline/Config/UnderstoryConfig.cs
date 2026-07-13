using UnityEngine;

namespace MapGenerator.Pipeline.Config
{
    /// <summary>
    /// 下層木クラスタリング・独立散布のバイオーム別設定。
    /// AddUnderstoryClustersAroundCanopy と ScatterUnderstoryIndependent が参照する。
    /// </summary>
    [System.Serializable]
    public class UnderstoryConfig
    {
        // 下層木判定
        [Header("下層木判定")]
        [Label("下層木スケール閾値")]
        [Range(0.3f, 1.5f)] public float understoryScaleThreshold = 0.80f;

        [Label("下層木最小間隔(m)")]
        [Range(0.5f, 5f)] public float understoryNeighborRadius = 2.0f;

        // 巨木周囲クラスター
        [Header("巨木周囲クラスター")]
        [Label("密林パッチ数(min)")]
        [Range(1, 5)] public int densePatches = 2;
        [Label("密林パッチ数(追加max)")]
        [Range(0, 4)] public int densePatchesRandom = 2;

        [Label("遷移帯パッチ数(min)")]
        [Range(0, 4)] public int transitionPatches = 1;
        [Label("遷移帯パッチ数(追加max)")]
        [Range(0, 4)] public int transitionPatchesRandom = 2;

        [Label("密林本数(min)")]
        [Range(2, 15)] public int denseTreesPerCanopy = 6;
        [Label("密林本数(追加max)")]
        [Range(0, 10)] public int denseTreesRandom = 5;

        [Label("遷移帯本数(min)")]
        [Range(1, 10)] public int transitionTreesPerCanopy = 4;
        [Label("遷移帯本数(追加max)")]
        [Range(0, 8)] public int transitionTreesRandom = 3;

        [Label("パッチ距離(min, m)")]
        [Range(2f, 20f)] public float patchDistanceMin = 5f;
        [Label("パッチ距離(max, m)")]
        [Range(5f, 30f)] public float patchDistanceMax = 12f;

        [Label("密林パッチ半径(min, m)")]
        [Range(1f, 8f)] public float densePatchRadiusMin = 3.0f;
        [Label("密林パッチ半径(max, m)")]
        [Range(2f, 10f)] public float densePatchRadiusMax = 4.5f;

        [Label("遷移帯パッチ半径(min, m)")]
        [Range(1f, 6f)] public float transitionPatchRadiusMin = 2.5f;
        [Label("遷移帯パッチ半径(max, m)")]
        [Range(1.5f, 8f)] public float transitionPatchRadiusMax = 3.8f;

        [Label("密林マスク閾値")]
        [Range(0.1f, 0.7f)] public float denseMaskThreshold = 0.34f;
        [Label("遷移帯マスク閾値")]
        [Range(0.1f, 0.7f)] public float transitionMaskThreshold = 0.41f;

        [Label("傾斜制限(°)")]
        [Range(10f, 45f)] public float understorySlopeLimit = 24f;

        // 独立散布
        [Header("独立散布")]
        [Label("散布最小距離(m)")]
        [Range(8f, 40f)] public float scatterMinDistance = 20f;

        [Label("散布密度係数")]
        [Range(0.3f, 1f)] public float scatterDensityMultiplier = 0.7f;

        [Label("散布確率(min)")]
        [Range(0.05f, 0.5f)] public float scatterProbMin = 0.25f;
        [Label("散布確率(max)")]
        [Range(0.3f, 1f)] public float scatterProbMax = 0.60f;

        [Label("散布傾斜制限(°)")]
        [Range(10f, 45f)] public float scatterSlopeLimit = 25f;

        [Label("クラスタサイズ(min)")]
        [Range(2, 15)] public int scatterClusterSize = 6;
        [Label("クラスタサイズ(追加max)")]
        [Range(0, 20)] public int scatterClusterSizeRandom = 10;

        [Label("クラスタ半径(min, m)")]
        [Range(2f, 20f)] public float scatterClusterRadiusMin = 5f;
        [Label("クラスタ半径(追加max, m)")]
        [Range(0f, 15f)] public float scatterClusterRadiusRandom = 5f;

        [Label("個体間最小距離(m)")]
        [Range(0.5f, 5f)] public float scatterNeighborRadius = 1.5f;

        // パッチ・クラスターの楕円アスペクト比
        [Header("アスペクト比")]
        [Label("パッチアスペクト最小")]
        [Range(0.3f, 1.5f)] public float patchAspectMin = 0.7f;

        [Label("パッチアスペクト最大")]
        [Range(0.5f, 2f)] public float patchAspectMax = 1.15f;

        [Label("散布アスペクト最小")]
        [Range(0.3f, 1.5f)] public float scatterAspectMin = 0.6f;

        [Label("散布アスペクト最大")]
        [Range(0.5f, 2f)] public float scatterAspectMax = 1.0f;

        // --- AddUnderstoryClustersAroundCanopy 用マスクパラメータ ---
        [Header("パッチマスクノイズ")]
        [Label("パッチマスク周波数")]
        [Range(0.01f, 0.5f)] public float patchMaskFrequency = 0.18f;

        [Label("マスク重み（vs密度）")]
        [Range(0f, 1f)] public float patchMaskWeight = 0.75f;

        [Label("楕円マスクオフセット")]
        [Range(0f, 0.5f)] public float patchMaskEllipseOffset = 0.18f;

        [Header("パッチ内ターゲット数")]
        [Label("密林パッチ目標(min)")]
        [Range(1, 10)] public int patchTargetDense = 4;
        [Label("密林パッチ目標(追加max)")]
        [Range(0, 6)] public int patchTargetDenseRandom = 3;
        [Label("遷移パッチ目標(min)")]
        [Range(1, 8)] public int patchTargetTransition = 3;
        [Label("遷移パッチ目標(追加max)")]
        [Range(0, 6)] public int patchTargetTransitionRandom = 3;

        [Header("エッジスケール変調")]
        [Label("エッジスケール最大（中心側）")]
        [Range(0.8f, 1.5f)] public float edgeScaleMax = 1.1f;
        [Label("エッジスケール最小（外縁側）")]
        [Range(0.5f, 1.2f)] public float edgeScaleMin = 0.95f;

        // --- ScatterUnderstoryIndependent 用マスクパラメータ ---
        [Header("散布マスクノイズ")]
        [Label("散布マスク周波数")]
        [Range(0.01f, 0.5f)] public float scatterMaskFrequency = 0.12f;

        [Label("散布マスクブレンド下限")]
        [Range(0f, 1f)] public float scatterMaskBlendMin = 0.5f;

        [Label("散布マスクブレンド上限")]
        [Range(0f, 1f)] public float scatterMaskBlendMax = 1.0f;
    }
}
