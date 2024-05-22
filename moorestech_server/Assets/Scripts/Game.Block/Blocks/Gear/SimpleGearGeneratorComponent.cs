using Game.Block.Config.LoadConfig.Param;
using Game.Block.Interface.Component;
using Game.Gear.Common;

namespace Game.Block.Blocks.Gear
{
    public class SimpleGearGeneratorComponent : GearEnergyTransformer, IGearGenerator
    {
        public SimpleGearGeneratorComponent(SimpleGearGeneratorParam configParam, int entityId, IBlockConnectorComponent<IGearEnergyTransformer> connectorComponent) :
            base(0, entityId, connectorComponent)
        {
            TeethCount = configParam.TeethCount;
            GenerateRpm = configParam.GenerateRpm;
            GenerateTorque = configParam.GenerateTorque;
            GenerateIsClockwise = true;
        }
        public int TeethCount { get; }
        public float GenerateRpm { get; }
        public float GenerateTorque { get; }
        public bool GenerateIsClockwise { get; }
    }
}