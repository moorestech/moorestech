namespace Game.WorldMap
{
    /// <summary>
    ///     
    ///     
    ///     ID
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