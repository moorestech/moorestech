using System;
using Game.EnergySystem;
using Game.Gear.Common;
using MessagePack;

namespace Game.Block.Blocks.ElectricToGear
{
    [Serializable]
    [MessagePackObject]
    public class ElectricToGearGeneratorBlockStateDetail : GearStateDetail
    {
        public const string BlockStateDetailKey = "ElectricToGearGenerator";

        [Key(7)] public int SelectedIndex { get; set; }
        [Key(8)] public float ElectricFulfillmentRate { get; set; }
        [Key(9)] public float ConsumedElectricPower { get; set; }

        public ElectricToGearGeneratorBlockStateDetail(
            bool isClockwise,
            RPM currentRpm,
            Torque currentTorque,
            int selectedIndex,
            float electricFulfillmentRate,
            ElectricPower consumedPower) :
            base(isClockwise, currentRpm.AsPrimitive(), currentTorque.AsPrimitive())
        {
            SelectedIndex = selectedIndex;
            ElectricFulfillmentRate = electricFulfillmentRate;
            ConsumedElectricPower = consumedPower.AsPrimitive();
        }

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public ElectricToGearGeneratorBlockStateDetail()
        {
        }
    }
}
