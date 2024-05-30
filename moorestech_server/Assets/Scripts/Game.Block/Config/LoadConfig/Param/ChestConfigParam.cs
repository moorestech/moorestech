using Game.Block.Interface.BlockConfig;

namespace Game.Block.Config.LoadConfig.Param
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