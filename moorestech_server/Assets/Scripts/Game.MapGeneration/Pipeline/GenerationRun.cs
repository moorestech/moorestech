using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Visual.Placement;

namespace Game.MapGeneration.Pipeline
{
    // 生成1回が産む成果一式。3つとも必須コンストラクタ引数なので、詰め忘れはコンパイルエラーになる。
    // Everything a single generation run yields; all three are mandatory constructor arguments, so a missing one fails to compile.
    public sealed class GenerationRun
    {
        // 生成の結果出力。map.json・terrainファイル・転送メタはここからだけ作る。
        // The result output; map.json, the terrain files, and the transfer meta are built from this alone.
        public MapGenerationOutput Output { get; }

        // pass-1(配置)からpass-2(見た目)への受け渡し専用。結果出力(MapInfoJsonBuilder等)には一切写さない。
        // Internal pass-1(placement) to pass-2(visuals) handoff only; never copied into the result output (MapInfoJsonBuilder etc.).
        public PlacementLedger Ledger { get; }

        // 生成器がスポーン探索の結果を書き戻したあとのConfig。pass-2は入力のConfigではなくこちらを使う。
        // The config after the generator wrote the spawn-search result back; pass-2 uses this rather than the input config.
        public TerrainGenerationConfig Config { get; }

        public GenerationRun(MapGenerationOutput output, PlacementLedger ledger, TerrainGenerationConfig config)
        {
            Output = output;
            Ledger = ledger;
            Config = config;
        }
    }
}
