using UnityEngine;

namespace MapGenerator.Pipeline.Config
{
    /// <summary>
    /// 海岸線と水際判定の共通設定。
    /// ビーチ遷移と水際配置の基準をTerrainGenerationConfigから一括で供給する。
    /// </summary>
    [System.Serializable]
    public class BiomeShoreConfig
    {
        // 樹木・岩石・草花がseaLevel+waterMarginより低い場所に配置されないマージン
        [Header("水際配置")]
        [Label("水際配置マージン")]
        [Range(0f, 0.1f)] public float waterMargin = 0.03f;

        // ビーチの海面からの隆起量。HeightSampleJobが海岸線の上端高さとして使う
        [Header("ビーチ遷移")]
        [Label("砂浜隆起量")]
        [Range(0.001f, 0.03f)] public float beachElevation = 0.0058f;

        [Label("陸側テクスチャ幅(px)")]
        [Range(0, 60)] public int beachLandTextureRadius = 16;

        [Label("陸側地形幅(px)")]
        [Range(0, 60)] public int beachLandTerrainRadius = 10;

        [Label("海側テクスチャ幅(px)")]
        [Range(0, 60)] public int beachSeaTextureRadius = 14;

        [Label("海側地形幅(px)")]
        [Range(1, 60)] public int beachSeaTerrainRadius = 11;

        // Ocean/Beachスプラットの0番レイヤーとして常に使う共通砂浜テクスチャ
        [Label("砂浜テクスチャ")]
        public TerrainLayer beachLayer;

        // ビーチ遷移帯の判定閾値
        [Header("判定閾値")]
        [Label("ビーチ判定閾値")]
        [Range(0.001f, 0.1f)] public float beachThreshold = 0.01f;

        [Label("深海判定閾値")]
        [Range(0.001f, 0.05f)] public float deepSeaThreshold = 0.005f;

        [Label("砂ブレンド閾値")]
        [Range(0.001f, 0.1f)] public float sandBlendThreshold = 0.01f;

        // テクスチャ重み0ピクセルのフォールバックレイヤーIndex
        [Label("岩フォールバックレイヤー")]
        public int rockFallbackLayerIndex = 1;

        // この面積未満の海の連結成分を陸に変換し、細かなギザギザ海域を潰す
        [Label("最小海域サイズ")]
        [Range(0, 10000)] public int minSeaRegionSize = 3672;
    }
}
