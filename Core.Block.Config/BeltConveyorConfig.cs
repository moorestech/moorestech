namespace Core.Block.Config
{
    public static class BeltConveyorConfig
    {
        private const int DefaultBeltConveyorNum = 4;
        private const int DefaultTimeOfItemEnterToExit = 2000;
        //TODO ベルトコンベアもBlockFactoryから生成するようにする
        public static BeltConveyorData GetBeltConveyorData(int blockId)
        {
            return new BeltConveyorData(DefaultBeltConveyorNum,DefaultTimeOfItemEnterToExit);
        }
    }

    public class BeltConveyorData
    {
        public BeltConveyorData(int beltConveyorItemNum, int timeOfItemEnterToExit)
        {
            BeltConveyorItemNum = beltConveyorItemNum;
            TimeOfItemEnterToExit = timeOfItemEnterToExit;
        }

        public int BeltConveyorItemNum { get; }
        public int TimeOfItemEnterToExit { get; }
    }
}