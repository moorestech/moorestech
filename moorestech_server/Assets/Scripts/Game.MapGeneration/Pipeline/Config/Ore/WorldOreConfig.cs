namespace Game.MapGeneration.Pipeline.Config
{
    // ワールド全体で共通の鉱脈配置設定。item 鉱脈(entries)と fluid 鉱脈(fluidEntries)を保持する。
    // World-wide vein config holding item veins (entries) and fluid veins (fluidEntries).
    public class WorldOreConfig
    {
        public OreEntry[] entries = new OreEntry[0];
        // fluid 鉱脈は同形。配置ロジックは P5（5a では保持のみ）。
        // Fluid veins share the same shape; placement is P5 (only held here in 5a).
        public OreEntry[] fluidEntries = new OreEntry[0];
        public float borderMargin = 5f;
    }
}
