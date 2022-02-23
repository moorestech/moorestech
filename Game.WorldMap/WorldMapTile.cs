namespace Game.WorldMap
{
    /// <summary>
    /// マップタイルを取得します
    /// 鉱石がある場合は鉱石をかえし、ない場合は空のマップタイルを返します
    /// 今後はバイオームに応じたIDを返します
    /// </summary>
    public class WorldMapTile
    {
        private readonly VeinGenerator _generator;

        public WorldMapTile(VeinGenerator generator)
        {
            _generator = generator;
        }

        public int GetMapTile(int x, int y)
        {
            return _generator.GetOreId(x, y);
        }
    }
}