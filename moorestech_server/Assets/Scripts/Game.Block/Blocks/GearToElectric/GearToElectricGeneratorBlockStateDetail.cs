using System;
using Game.EnergySystem;
using Game.Gear.Common;
using MessagePack;

namespace Game.Block.Blocks.GearToElectric
{
    [Serializable]
    [MessagePackObject]
    public class GearToElectricGeneratorBlockStateDetail : GearStateDetail
    {
        public const string GearGeneratorBlockStateDetailKey = "GearToElectricGenerator";

        // 充電率（バッテリー残量 / 容量）
        // Charge rate (battery remaining / capacity)
        [Key(7)] public float ChargeRate { get; set; }

        // 直近の電力tickで実際に放電（発電）した電力
        // Power actually discharged (generated) on the latest electric tick
        [Key(8)] public float GeneratedElectricPower { get; set; }

        [Key(9)] public float BatteryRemaining { get; set; }

        public GearToElectricGeneratorBlockStateDetail(
            bool isClockwise,
            RPM currentRpm,
            Torque currentTorque,
            float chargeRate,
            ElectricPower generatedPower,
            float batteryRemaining) :
            base(isClockwise, currentRpm.AsPrimitive(), currentTorque.AsPrimitive())
        {
            ChargeRate = chargeRate;
            GeneratedElectricPower = generatedPower.AsPrimitive();
            BatteryRemaining = batteryRemaining;
        }

        [Obsolete("Deserialize only")]
        public GearToElectricGeneratorBlockStateDetail()
        {
        }
    }
}
