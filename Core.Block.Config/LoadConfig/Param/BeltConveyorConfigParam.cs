namespace Core.Block.Config.LoadConfig.Param
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
    }
}