using MapGenerator.Pipeline.Biomes;
using UnityEngine;

namespace MapGenerator.Pipeline.Config
{
    /// <summary>
    /// 1種類の鉱脈エントリ。スポーン地点からの距離バンド(bands)ごとに
    /// Poissonクラスター配置のパラメータを切り替える。
    /// prefab・傾斜フィルタ・他オブジェクトとの最小距離はバンド共通。
    /// WorldOreConfig.entries[] から参照され、OrePlacementGenerator が消費する。
    /// </summary>
    [System.Serializable]
    public class OreEntry
    {
        [Label("プレハブ")]
        public GameObject prefab;

        // この鉱石を配置するバイオーム（LayerMask風の複数選択ビットマスク）。配置候補の位置の
        // バイオームがこのマスクに含まれる場合のみ残す。None の場合はどこにも配置しない。
        [Label("配置されるバイオーム")]
        public BiomeFlags biomes = BiomeFlags.None;

        // スポーン地点からの距離バンド。各リングで密度・クラスター規模・配置間隔を変える。
        // OreBandPlanner が outerRadiusMeters 昇順（-1=無限は末尾）に並べて消費する。
        [Label("距離バンド")]
        public OreBand[] bands = new OreBand[0];

        // 急斜面を避けて平坦な場所に鉱脈を配置する（バンド共通）
        [Label("傾斜フィルタ有効")]
        public bool useSlopeFilter = true;

        [Label("傾斜上限(°)")]
        [Range(0f, 90f)]
        public float slopeMax = 30f;

        [Label("傾斜スムーズ幅")]
        [Range(0f, 20f)]
        public float slopeSmoothness = 5f;

        // 樹木・岩など既存配置物との最小距離(m)（バンド共通）
        [Label("他オブジェクトからの最小距離(m)")]
        [Range(0f, 30f)]
        public float minDistanceFromOthers = 3f;
    }
}
