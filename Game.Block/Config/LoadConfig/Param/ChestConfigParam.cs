namespace Core.Block.Config.LoadConfig.Param
{
    public class ChestConfigParam : IBlockConfigParam
    {
        public ChestConfigParam(int chestItemNum)
        {
            ChestItemNum = chestItemNum;
        }

        public int ChestItemNum { get; }
    }
}