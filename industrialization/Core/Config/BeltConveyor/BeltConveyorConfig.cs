namespace industrialization.Core.Config.BeltConveyor
{
    public static class BeltConveyorConfig
    {
        private const int DefaultBeltConveyorNum = 4;
        private const int DefaultTimeOfItemEnterToExit = 200;
        //TODO テスト用仮メソッドを実装する
        public static BeltConveyorData GetBeltConveyorData(int installtionID)
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