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

        [Key(7)] public float EnergyFulfillmentRate { get; set; }
        [Key(8)] public float GeneratedElectricPower { get; set; }

        public GearToElectricGeneratorBlockStateDetail(
            bool isClockwise,
            RPM currentRpm,
            Torque currentTorque,
            float energyFulfillmentRate,
            ElectricPower generatedPower) :
            base(isClockwise, currentRpm.AsPrimitive(), currentTorque.AsPrimitive())
        {
            EnergyFulfillmentRate = energyFulfillmentRate;
            GeneratedElectricPower = generatedPower.AsPrimitive();
        }

        [Obsolete("Deserialize only")]
        public GearToElectricGeneratorBlockStateDetail()
        {
        }
    }
}
