namespace industrialization.Core.Config.BeltConveyor
{
    public class BeltConveyorConfig
    {
        private const int DefaultBeltConveyorNum = 4;
        private const int DefaultBeltConveyorSpeed = 200;
        //TODO テスト用仮メソッドを実装する
        public static BeltConveyorData GetBeltConveyorData(int installtionID)
        {
            return new BeltConveyorData(DefaultBeltConveyorNum,DefaultBeltConveyorSpeed);
        }
    }

    public class BeltConveyorData
    {
        public BeltConveyorData(int beltConveyorItemNum, int beltConveyorSpeed)
        {
            BeltConveyorItemNum = beltConveyorItemNum;
            BeltConveyorSpeed = beltConveyorSpeed;
        }

        public int BeltConveyorItemNum { get; }
        public int BeltConveyorSpeed { get; }
    }
}