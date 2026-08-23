using Game.MapGeneration.Pipeline.Visual;

namespace Game.MapGeneration.Facade
{
    /// <summary>
    ///     生成タイルを焼けるセッション。bakerが必須コンストラクタ引数なので、焼けないセッションはこの型になり得ない
    ///     A session that can bake generated tiles; the baker being a mandatory constructor argument, a non-bakeable session can never take this type
    /// </summary>
    public sealed class TiledTerrainSession : WorldTerrainSession
    {
        private readonly TileVisualBaker _baker;

        // bakerは生成システム内部の型。外から組み立てられないようinternalで閉じ、入口はWorldTerrainSession.Openだけに保つ
        // The baker is a generation-internal type: internal keeps outside assemblies from assembling one, leaving WorldTerrainSession.Open as the only entry
        internal TiledTerrainSession(WorldTerrainLayout layout, TileVisualBaker baker) : base(layout)
        {
            _baker = baker;
        }

        public BakedTerrainTile BakeTile(int tileX, int tileZ)
        {
            // 生成内部の結果を境界DTOへ値のまま移すだけ。ここで計算を挟むと焼き上がりの正本が2箇所になる
            // Merely moves the generation-internal result into the boundary DTO as values; computing here would give the bake two sources of truth
            var bakeResult = _baker.Bake(tileX, tileZ);
            return new BakedTerrainTile(
                bakeResult.ScenePosition, bakeResult.DisplayHeights, bakeResult.Alphamap, bakeResult.DetailMaps);
        }
    }
}
