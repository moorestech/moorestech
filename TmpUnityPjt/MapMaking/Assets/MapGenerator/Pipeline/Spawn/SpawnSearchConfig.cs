using UnityEngine;

namespace MapGenerator.Pipeline.Spawn
{
    /// <summary>
    /// スポーン候補探索プリパスのパラメータ。TerrainGenerationConfig に内包される。
    /// 面積はすべて m² 単位（セル数だと解像度変更で意味が壊れるため）。
    /// </summary>
    [System.Serializable]
    public class SpawnSearchConfig
    {
        [Header("スポーン描画位置（シーン座標）")]
        [Label("描画位置を指定する")]
        [Tooltip("ONなら下のシーン座標にスポーン地点を描画する。OFFならグリッドの幾何中心（自動, 例: 3x3×1000mなら500,500）")]
        public bool overrideSpawnScenePosition = false;
        [Label("スポーン描画位置(シーン座標 X,Z m)")]
        [Tooltip("中央化オフセットの打ち消し先。探索で見つけた良地を、実際にこのシーン座標へ描画する。本番サンプル格子に乗る値を推奨（乗らない場合は警告）")]
        public Vector2 spawnScenePosition = new Vector2(500f, 500f);

        [Header("段1: 粗探索")]
        [Min(1f)]
        [Tooltip("段1粗グリッドのセルサイズ(m)")]
        public float scanCellSize = 50f;
        [Min(0f)]
        [Tooltip("段1走査範囲(正方, m)。0以下なら生成グリッド外接を自動使用")]
        public float scanExtent = 0f;

        [Header("段2: 局所窓")]
        [Min(0f)]
        [Tooltip("段2局所窓の候補外接への追加マージン(m)")]
        public float windowMargin = 200f;
        [Label("段2窓の最大解像度(px)")]
        [Min(64)]
        [Tooltip("段2局所窓の一辺の最大サンプル数(px)。窓のピクセル数=この値^2で頭打ちにしOOMを防ぐ。メートル上限ではなくピクセル上限にすることで低解像度では大きな実メートル窓を許容しedgeMarginを満たせる")]
        public int maxDetailedResolution = 1600;

        [Header("合格条件 (m²/m)")]
        [Min(0f)]
        [Tooltip("草原連結成分の最小面積(m²)")]
        public float minGrasslandArea = 200000f;
        [Min(0f)]
        [Tooltip("森林連結成分の最小面積(m²)")]
        public float minForestArea = 150000f;
        [Min(0f)]
        [Tooltip("草原-森林 境界接触長の最小値(m)")]
        public float minBorderContact = 200f;

        [Header("スポーン点クリアランス (m)")]
        [Min(0f)]
        [Tooltip("スポーン点の非Grassland境界からの最小距離(m)")]
        public float grassClearanceMin = 30f;
        [Min(0f)]
        [Tooltip("スポーン点の海/Beachからの最小距離(m)")]
        public float waterClearanceMin = 60f;

        [Header("スコア重み")]
        [Label("草原面積の重み")]
        [Tooltip("草原連結成分の面積スコアに掛ける重み")]
        public float wGrasslandArea = 1f;
        [Label("森林面積の重み")]
        [Tooltip("森林連結成分の面積スコアに掛ける重み")]
        public float wForestArea = 0.5f;
        [Label("境界接触長の重み")]
        [Tooltip("草原-森林の境界接触長スコアに掛ける重み")]
        public float wBorderContact = 50f;
        [Label("内陸クリアランスの重み")]
        [Tooltip("スポーン点の最小クリアランススコアに掛ける重み")]
        public float wInland = 1f;

        [Header("探索制御")]
        [Min(1)]
        [Tooltip("段2検証の初期バッチ件数")]
        public int topK = 32;
        [Label("拡大率")]
        [Min(1.01f)]
        [Tooltip("候補ゼロ時のscanExtent拡大率")]
        public float expandFactor = 1.8f;
        [Label("最大拡大回数")]
        [Min(0)]
        [Tooltip("拡大走査の最大回数")]
        public int maxExpandIterations = 4;
    }
}
