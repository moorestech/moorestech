using UnityEngine;

namespace MapGenerator.Pipeline.Config
{
    /// <summary>
    /// ワールド全体で共通の鉱脈配置設定。TerrainGenerationConfig のフィールドとして保持される。
    /// 各 OreEntry が出現バイオーム(biomes)を持ち、OrePlacementGenerator が
    /// 対象バイオーム群の合成マスク内で entries[] を順次処理する。
    /// </summary>
    [System.Serializable]
    public class WorldOreConfig
    {
        [Label("鉱脈エントリ")]
        public OreEntry[] entries;

        // バイオーム境界（=対象バイオーム群の外縁）からこの距離(m)以内には配置しない
        [Label("境界マージン(m)")]
        [Range(0f, 20f)]
        public float borderMargin = 5f;
    }
}
