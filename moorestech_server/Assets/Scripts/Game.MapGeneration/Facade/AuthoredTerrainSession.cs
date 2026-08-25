namespace Game.MapGeneration.Facade
{
    /// <summary>
    ///     オーサリング済みの1枚地形を指すだけのセッション。焼くタイルを持たないので、焼く口も持たない
    ///     A session that merely points at one authored terrain; owning no tile to bake, it exposes no baking either
    /// </summary>
    public sealed class AuthoredTerrainSession : WorldTerrainSession
    {
        // 入口はWorldTerrainSession.Openだけに保つ
        // WorldTerrainSession.Open stays the only entry
        internal AuthoredTerrainSession(WorldTerrainLayout layout) : base(layout) { }
    }
}
