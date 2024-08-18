using Core.Item.Interface.Config;
using Game.Block.Interface.BlockConfig;

namespace Game.Block.Config.LoadConfig.Param
{
    public class NullBlockConfigParam : IBlockConfigParam
    {
        public static IBlockConfigParam Generate(dynamic blockParam, IItemConfig itemConfig)
        {
            return new NullBlockConfigParam();
        }
    }
}