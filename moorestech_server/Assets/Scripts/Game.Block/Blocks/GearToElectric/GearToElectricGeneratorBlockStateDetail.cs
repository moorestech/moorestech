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

        // 直近の電力tickで網へ渡した電力
        // Power handed to the network on the latest electric tick
        [Key(8)] public float GeneratedElectricPower { get; set; }
        // 発電状態
        // Generation state
        [Key(10)] public GearToElectricGenerationState GenerationState { get; set; }

        public GearToElectricGeneratorBlockStateDetail(
            bool isClockwise,
            RPM currentRpm,
            Torque currentTorque,
            ElectricPower suppliedPower,
            GearToElectricGenerationState generationState) :
            base(isClockwise, currentRpm.AsPrimitive(), currentTorque.AsPrimitive(), GearRole.Consumer)
        {
            GeneratedElectricPower = suppliedPower.AsPrimitive();
            GenerationState = generationState;
        }

        [Obsolete("Deserialize only")]
        public GearToElectricGeneratorBlockStateDetail()
        {
        }
    }
}
