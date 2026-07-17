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
        public new const string BlockStateDetailKey = "ElectricToGearGenerator";

        [Key(7)] public int SelectedIndex { get; set; }

        // 充電率（バッテリー残量 / 容量）
        // Charge rate (battery remaining / capacity)
        [Key(8)] public float ElectricFulfillmentRate { get; set; }

        // 直近の電力tickで充電に消費した電力
        // Power consumed for charging on the latest electric tick
        [Key(9)] public float ConsumedElectricPower { get; set; }

        [Key(10)] public float BatteryRemaining { get; set; }

        public ElectricToGearGeneratorBlockStateDetail(
            bool isClockwise,
            RPM currentRpm,
            Torque currentTorque,
            int selectedIndex,
            float chargeRate,
            ElectricPower consumedPower,
            float batteryRemaining) :
            base(isClockwise, currentRpm.AsPrimitive(), currentTorque.AsPrimitive())
        {
            SelectedIndex = selectedIndex;
            ElectricFulfillmentRate = chargeRate;
            ConsumedElectricPower = consumedPower.AsPrimitive();
            BatteryRemaining = batteryRemaining;
        }

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public ElectricToGearGeneratorBlockStateDetail()
        {
        }
    }
}
