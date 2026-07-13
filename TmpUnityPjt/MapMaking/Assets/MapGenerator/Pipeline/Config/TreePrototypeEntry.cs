using System;
using UnityEngine;

namespace MapGenerator.Pipeline.Config
{
    /// <summary>
    /// 独立した配置パイプラインを持つ樹種グループ。
    /// prefabs配列に複数プレハブを登録すると、配置時に等確率でランダム選択される。
    /// 各エントリが独自のdensityConfig/understoryConfig/rockProximityConfigを持ち、
    /// 他のエントリと独立して配置アルゴリズムを実行する。
    /// </summary>
    [Serializable]
    public class TreePrototypeEntry
    {
        // 同じ配置条件を共有する樹木プレハブ群。配置時に等確率で選択される
        [Label("プレハブ")]
        public GameObject[] prefabs;
        [Label("高さスケール範囲")]
        public Vector2 scaleHeightRange = new Vector2(0.8f, 1.2f);
        [Label("幅スケール範囲")]
        public Vector2 scaleWidthRange = new Vector2(0.8f, 1.2f);
        // trueなら幅スケールを高さスケ��ルに連動させ、プロポーション維持
        [Label("幅高さロック")]
        public bool lockWidthHeight = true;
        // 地面に沈める量（メートル単位、正規化座標ではない）
        [Label("沈み込み")]
        public float sink;
        // Unity TerrainのTreePrototype.bendFactorにセットされる風しなり量
        [Label("風しなり")]
        [Range(0f, 1f)] public float bendFactor;
        [Label("ランダム回転")]
        public bool randomRotation = true;
        // trueでこのプロトタイプをスキップ（プレハブを残したまま無効化）
        [Label("無効")]
        public bool disabled;

        // =====================================================
        // 配置アルゴリズム設定（エントリごとに独立）
        // =====================================================

        [Header("密度・配置アルゴリズム")]
        [Label("樹木密度設定")]
        public TreeDensityConfig densityConfig = new TreeDensityConfig();

        [Header("下層木")]
        [Label("下層木設定")]
        public UnderstoryConfig understoryConfig = new UnderstoryConfig();

        [Header("岩周辺樹木")]
        [Label("岩周辺樹木設定")]
        public RockProximityTreeConfig rockProximityConfig = new RockProximityTreeConfig();

        // バイオーム境界からこの距離(m)以内には樹木を配置しない
        [Header("境界マージン")]
        [Label("境界マージン(m)")]
        [Range(0f, 20f)] public float borderMargin = 0f;

        // 他エントリ・他パスの木との最小距離。sharedGridで全配置済み木と照合する
        [Header("共有グリッド距離")]
        [Label("最小近傍距離(m)")]
        [Range(1f, 30f)] public float sharedGridMinDistance = 2f;

        // =====================================================
        // プロ���タイプ別フィルタ・ノイズ
        // =====================================================

        // PlacementFilter.enabled=true で個別フィルタが有効になり、
        // 配置ポイントで重みを変調。範囲外なら配置確率0
        [Header("傾斜フィルタ")]
        [Label("傾斜フィルタ")]
        public PlacementFilter slopeFilter;
        [Header("曲率フィルタ")]
        [Label("曲率フィルタ")]
        public PlacementFilter curvatureFilter;

        // バイオーム全体のクラスタリングとは独立に、この樹種専用の空間分布を定義
        [Header("クラスタリングノイズ")]
        [Label("第1ノイズ")]
        public PlacementNoise clusterNoise;
        [Label("第1ノイズ閾値")]
        [Range(0f, 1f)] public float clusterNoiseThreshold = 0.3f;
        [Label("第2ノイズ")]
        public PlacementNoise clusterNoise2;
        [Label("第2ノイズ演算子")]
        public NoiseOp noise2Op = NoiseOp.Multiply;

        // 配置された木の周辺の地形高さをガウシアンで変更
        [Header("地形変更")]
        [Label("地形変更量")]
        [Range(-3f, 3f)] public float heightModAmount;
        [Label("地形変更幅")]
        [Range(0.1f, 20f)] public float heightModWidth = 2f;

        // 配置された木の周辺にテクスチャレイヤーをブレンド（根元の土など）
        [Header("テクスチャ変更")]
        [Label("周囲テクスチャ")]
        public TerrainLayer surroundLayer;
        [Label("テクスチャ重み")]
        [Range(0f, 1f)] public float surroundLayerWeight;
        [Label("テクスチャ幅")]
        [Range(0.1f, 20f)] public float surroundLayerWidth = 2f;

        // バイオーム境界でのスケール抑制
        [Header("バイオーム境界")]
        [Label("境界スケール倍率")]
        [Range(0f, 1f)] public float boundaryScaleMultiplier = 1f;

        // 一定確率で巨木として大スケール配置
        [Header("巨木")]
        [Label("巨木スケール倍率")]
        [Range(1f, 5f)] public float oldGrowthScale = 1f;
        [Label("巨木出現率")]
        [Range(0f, 1f)] public float oldGrowthRatio;
    }
}
