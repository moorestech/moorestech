using System;

namespace industrialization.Config.BeltConveyor
{
    public class BeltConveyorConfig
    {
        /// <summary>
        /// テスト用のセットアップ仮メソッド
        /// </summary>
        [Obsolete("テスト専用です。テスト以外の用途では使わないでください。")]
        public static void TestSetBeltConveyorNum(int _speed,int _num)
        {
            speed = _speed;
            num = _num;
        }

        private static int speed;
        private static int num;
        //TODO テスト用仮メソッドを実装する
        public static BeltConveyorData GetBeltConveyorData(int installtionID)
        {
            return new BeltConveyorData(num,speed);
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