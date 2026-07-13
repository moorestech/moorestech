using UnityEngine;

namespace MapGenerator.Pipeline.Config
{
    /// <summary>
    /// 樹木密度分布のバイオーム別アルゴリズム設定。
    /// TreePlacementGenerator の3層構造（Dense/Transition/Sparse）と
    /// 傾斜・岩近接フィルタのパラメータを制御する。
    /// </summary>
    [System.Serializable]
    public class TreeDensityConfig
    {
        // 密度閾値: 3層構造の境界を定義
        [Header("密度閾値")]
        [Label("密林閾値")]
        [Range(0.2f, 0.8f)] public float denseMinThreshold = 0.45f;

        [Label("遷移帯閾値")]
        [Range(0.1f, 0.6f)] public float transitionMinThreshold = 0.26f;

        // パス別配置距離: Poisson Disk の最小距離下限
        [Header("パス別配置距離")]
        [Label("密林パス最小距離(m)")]
        [Range(5f, 30f)] public float densePassMinDistance = 15.0f;

        [Label("遷移パス最小距離(m)")]
        [Range(3f, 20f)] public float transitionPassMinDistance = 8.0f;

        [Label("疎林パス最小距離(m)")]
        [Range(10f, 50f)] public float sparsePassMinDistance = 20f;

        [Label("散在パス最小距離(m)")]
        [Range(10f, 50f)] public float scatterPassMinDistance = 22f;

        // 遷移帯の確率カーブ
        [Header("遷移帯確率")]
        [Label("遷移帯ベース確率")]
        [Range(0f, 0.3f)] public float transitionBaseProb = 0.06f;

        [Label("遷移帯ピーク確率")]
        [Range(0.3f, 1f)] public float transitionPeakProb = 0.74f;

        [Label("遷移帯確率べき乗")]
        [Range(0.5f, 3f)] public float transitionProbPower = 1.5f;

        // 草地リジェクト
        [Header("草地")]
        [Label("草地リジェクト係数")]
        [Range(0.1f, 1f)] public float sparseOpenRejectFactor = 0.6f;

        [Label("散在ベース確率")]
        [Range(0f, 0.1f)] public float scatterBaseProb = 0.02f;

        [Label("散在密度係数")]
        [Range(0.05f, 0.5f)] public float scatterDensityFactor = 0.25f;

        // 傾斜リジェクト
        [Header("傾斜制限")]
        [Label("完全棄却角度(°)")]
        [Range(15f, 60f)] public float slopeHardReject = 30f;

        [Label("間引き開始角度(°)")]
        [Range(10f, 45f)] public float slopeSoftReject = 20f;

        // 岩近接ロジック
        [Header("岩近接")]
        [Label("棄却距離(m)")]
        [Range(1f, 10f)] public float rockRejectDistance = 5f;

        [Label("棄却確率")]
        [Range(0.5f, 1f)] public float rockRejectProb = 0.9f;

        [Label("ブースト近距離(m)")]
        [Range(5f, 25f)] public float rockBoostNearDistance = 15f;

        [Label("ブースト遠距離(m)")]
        [Range(15f, 50f)] public float rockBoostFarDistance = 25f;

        [Label("遠距離棄却確率")]
        [Range(0f, 0.8f)] public float rockFarRejectProb = 0.5f;

        // 3スケール密度ノイズの周波数。バイオームごとの森林クラスタパターンを制御
        [Header("密度ノイズ")]
        [Label("大スケール周波数")]
        public float densityLargeFrequency = 0.007f;

        [Label("中スケール周波数")]
        public float densityMidFrequency = 0.013f;

        [Label("小スケール周波数")]
        public float densitySmallFrequency = 0.028f;

        [Label("大スケール重み")]
        [Range(0f, 1f)] public float densityLargeWeight = 0.40f;

        [Label("中スケール重み")]
        [Range(0f, 1f)] public float densityMidWeight = 0.40f;

        [Label("小スケール重み")]
        [Range(0f, 1f)] public float densitySmallWeight = 0.20f;

        // 密度の最低保証値。森林等で無木地帯を防ぐ
        [Label("密度下限")]
        [Range(0f, 0.5f)] public float densityFloor = 0f;

        // 島変調ノイズ: 密林島パッチの空間スケールと減衰強度
        [Label("島変調周波数")]
        public float islandModulationFrequency = 0.02f;

        [Label("島変調下限")]
        [Range(0f, 1f)] public float islandModulationMin = 0.78f;

        [Label("島変調上限")]
        [Range(0f, 1f)] public float islandModulationMax = 1.0f;

        // 巨木/下層木判定のスケール境界
        [Label("巨木スケール閾値")]
        [Range(0.3f, 2f)] public float canopyScaleThreshold = 1f;

        // Poisson Diskの密度倍率。totalDesired×倍率で各パスのポイント数を決定
        [Header("パス密度倍率")]
        [Label("密林パス倍率")]
        [Range(0.5f, 10f)] public float densePassMultiplier = 4f;

        [Label("遷移パス倍率")]
        [Range(0.1f, 5f)] public float transitionPassMultiplier = 1.05f;

        [Label("疎林パス倍率")]
        [Range(0.01f, 1f)] public float sparsePassMultiplier = 0.06f;

        // 局所密度から配置確率への変換パラメータ
        [Header("密度変調")]
        [Label("密度変調下限")]
        [Range(0f, 1f)] public float densityModMin = 0.3f;

        [Label("密度変調上限")]
        [Range(0f, 1f)] public float densityModMax = 1.0f;

        [Label("密度変調スケール")]
        [Range(0.5f, 3f)] public float densityModScale = 1.5f;

        [Label("保持確率(近距離)")]
        [Range(0f, 1f)] public float keepProbNear = 0.95f;

        [Label("保持確率(遠距離)")]
        [Range(0f, 1f)] public float keepProbFar = 0.15f;

        // 局所密度上限: 半径内の木の本数が上限を超えたら間引く
        [Header("局所密度上限")]
        [Label("密度上限チェック半径(m)")]
        [Range(5f, 50f)] public float localDensityCapRadius = 20f;

        [Label("半径内の最大本数")]
        [Range(1, 30)] public int localDensityCapCount = 8;
    }
}
