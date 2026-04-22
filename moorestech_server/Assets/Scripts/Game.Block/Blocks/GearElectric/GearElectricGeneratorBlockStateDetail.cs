using System;
using Game.EnergySystem;
using Game.Gear.Common;
using MessagePack;

namespace Game.Block.Blocks.GearElectric
{
    [Serializable]
    [MessagePackObject]
    public class GearElectricGeneratorBlockStateDetail : GearStateDetail
    {
        public const string GearGeneratorBlockStateDetailKey = "GearElectricGenerator";

        [Key(7)] public float EnergyFulfillmentRate { get; set; }
        [Key(8)] public float GeneratedElectricPower { get; set; }

        public GearElectricGeneratorBlockStateDetail(
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
        public GearElectricGeneratorBlockStateDetail()
        {
        }
    }
}
