using Game.MapGeneration.Pipeline.Biomes;

namespace Game.MapGeneration.Pipeline.Config
{
    // 1種類の鉱脈エントリ。prefab はスキーマ化で veinGuid（mapVeins 参照）へ置換した。
    // A single vein entry; prefab replaced by veinGuid (mapVeins reference) via schema migration.
    public class OreEntry
    {
        // 配置対象の mapVeins veinGuid（文字列）。鉱石岩の見た目は出力しない（ADR#10）。
        // Target mapVeins veinGuid (string); no ore-rock visual is emitted (ADR#10).
        public string veinGuid;
        public BiomeFlags biomes = BiomeFlags.None;
        public OreBand[] bands = new OreBand[0];
        public bool useSlopeFilter = true;
        public float slopeMax = 30f;
        public float slopeSmoothness = 5f;
        public float minDistanceFromOthers = 3f;
    }
}
