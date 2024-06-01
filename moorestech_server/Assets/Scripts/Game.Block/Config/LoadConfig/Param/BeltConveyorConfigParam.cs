using Core.Item.Interface.Config;
using Game.Block.Interface.BlockConfig;

namespace Game.Block.Config.LoadConfig.Param
{
    public class BeltConveyorConfigParam : IBlockConfigParam
    {
        public BeltConveyorConfigParam(int timeOfItemEnterToExit, int beltConveyorItemNum)
        {
            TimeOfItemEnterToExit = timeOfItemEnterToExit;
            BeltConveyorItemNum = beltConveyorItemNum;
        }
        
        public int BeltConveyorItemNum { get; }
        public int TimeOfItemEnterToExit { get; }
        
        public static IBlockConfigParam Generate(dynamic blockParam, IItemConfig itemConfig)
        {
            int slot = blockParam.slot;
            int time = blockParam.time;
            
            return new BeltConveyorConfigParam(time, slot);
        }
    }
}