using Core.Item.Interface.Config;
using Game.Block.Interface.BlockConfig;

namespace Game.Block.Config.LoadConfig.Param
{
    public class GearBeltConveyorConfigParam : IBlockConfigParam
    {
        public GearBeltConveyorConfigParam(int beltConveyorItemNum, int timeOfItemEnterToExit, float requiredTorque)
        {
            BeltConveyorItemNum = beltConveyorItemNum;
            TimeOfItemEnterToExit = timeOfItemEnterToExit;
            RequiredTorque = requiredTorque;
        }
        public int BeltConveyorItemNum { get; }
        public int TimeOfItemEnterToExit { get; }
        public float RequiredTorque { get; }
        
        
        public static IBlockConfigParam Generate(dynamic blockParam, IItemConfig itemConfig)
        {
            int slot = blockParam.slot;
            int time = blockParam.time;
            float requiredTorque = blockParam.requiredTorque;
            
            return new GearBeltConveyorConfigParam(slot, time, requiredTorque);
        }
    }
}