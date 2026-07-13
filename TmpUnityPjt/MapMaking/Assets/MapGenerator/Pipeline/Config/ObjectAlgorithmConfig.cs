using UnityEngine;

namespace MapGenerator.Pipeline.Config
{
    /// <summary>
    /// オブジェクト配置のバイオーム別アルゴリズム設定。
    /// バイオーム重み閾値、ヒーロー岩形状、従属岩配置、瓦礫パッチの定数を制御する。
    /// </summary>
    [System.Serializable]
    public class ObjectAlgorithmConfig
    {
        // ヒーロー岩
        [Header("ヒーロー岩")]
        [Label("中心オフセット係数")]
        [Range(0.1f, 0.6f)] public float heroOffsetFactor = 0.3f;

        [Label("スケール下限比率")]
        [Range(0.4f, 0.9f)] public float heroScaleMinRatio = 0.7f;

        [Label("スケール幅")]
        [Range(0.1f, 0.5f)] public float heroScaleRange = 0.3f;

        [Label("Y比率(min)")]
        [Range(0.4f, 1f)] public float heroYScaleMin = 0.7f;

        [Label("Y比率幅")]
        [Range(0f, 0.3f)] public float heroYScaleRange = 0.15f;

        // 従属岩
        [Header("従属岩")]
        [Label("距離係数(min)")]
        [Range(0.1f, 0.6f)] public float subordinateDistMin = 0.35f;

        [Label("距離係数幅")]
        [Range(0.2f, 1f)] public float subordinateDistRange = 0.65f;

        [Label("角度リジェクト(°)")]
        [Range(30f, 90f)] public float subordinateAngleReject = 55f;

        [Label("スケール上限比率")]
        [Range(0.3f, 0.9f)] public float subordinateScaleMaxRatio = 0.6f;

        [Label("Y比率(min)")]
        [Range(0.3f, 0.8f)] public float subordinateYScaleMin = 0.5f;

        [Label("Y比率幅")]
        [Range(0.1f, 0.5f)] public float subordinateYScaleRange = 0.3f;

        // 瓦礫パッチ
        [Header("瓦礫パッチ")]
        [Label("サドル配置確率")]
        [Range(0.2f, 0.9f)] public float saddleProbability = 0.65f;

        [Label("サドルジッター(m)")]
        [Range(0.5f, 8f)] public float saddleJitter = 3f;

        [Label("偏り扇形角度(×π)")]
        [Range(0.3f, 1f)] public float biasSectorAngle = 0.67f;

        [Label("サイズばらつき(min)")]
        [Range(0.2f, 0.8f)] public float rubbleSizeMin = 0.5f;

        [Label("サイズばらつき幅")]
        [Range(0.3f, 1.5f)] public float rubbleSizeRange = 1.0f;

        [Label("数量乗数")]
        [Range(1f, 15f)] public float rubbleDensityMultiplier = 5f;

        // Primaryクラスター中心間の最小距離係数（面積 / clusterCount * 係数 の平方根）
        [Header("クラスター間隔")]
        [Label("間隔係数")]
        [Range(0.1f, 2f)] public float clusterSpacingFactor = 0.6f;
    }
}
