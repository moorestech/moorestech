namespace Core.Ore
{
    /// <summary>
    /// 鉱石のコンフィグ
    /// </summary>
    public interface IOreConfig
    {
        public int OreIdToItemId(int oreId);
    }
}