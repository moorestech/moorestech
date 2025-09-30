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
        
        [Key(6)] public float EnergyFulfillmentRate { get; set; }
        [Key(7)] public float GeneratedElectricPower { get; set; }
        [Key(8)] public float InputRpm { get; set; }
        [Key(9)] public float InputTorque { get; set; }
        
        public GearElectricGeneratorBlockStateDetail(
            bool isClockwise,
            RPM currentRpm,
            Torque currentTorque,
            GearNetworkInfo gearNetworkInfo,
            float energyFulfillmentRate,
            ElectricPower generatedPower) :
            base(isClockwise, currentRpm.AsPrimitive(), currentTorque.AsPrimitive(), gearNetworkInfo)
        {
            EnergyFulfillmentRate = energyFulfillmentRate;
            GeneratedElectricPower = generatedPower.AsPrimitive();
            InputRpm = currentRpm.AsPrimitive();
            InputTorque = currentTorque.AsPrimitive();
        }
        
        [Obsolete("Deserialize only")]
        public GearElectricGeneratorBlockStateDetail()
        {
        }
    }
}
