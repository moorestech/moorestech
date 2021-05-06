using System;

namespace industrialization.Config.BeltConveyor
{
    public class BeltConveyorConfig
    {
        //TODO テスト用仮メソッドを実装する
        public static BeltConveyorData GetBeltConveyorData(int installtionID)
        {
            return new BeltConveyorData(4,200);
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