using Core.Item.Interface.Config;
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
        
        public static IBlockConfigParam Generate(dynamic blockParam, IItemConfig itemConfig)
        {
            int slot = blockParam.slot;
            
            return new ChestConfigParam(slot);
        }
    }
}