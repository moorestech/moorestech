using Game.Block.Config.LoadConfig.Param;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Gear.Common;

namespace Game.Block.Blocks.Gear
{
    public class SimpleGearGeneratorComponent : GearEnergyTransformer, IGearGenerator
    {
        public SimpleGearGeneratorComponent(SimpleGearGeneratorParam configParam, BlockInstanceId blockInstanceId, IBlockConnectorComponent<IGearEnergyTransformer> connectorComponent) :
            base(new Torque(0), blockInstanceId, connectorComponent)
        {
            TeethCount = configParam.TeethCount;
            GenerateRpm = configParam.GenerateRpm;
            GenerateTorque = configParam.GenerateTorque;
            GenerateIsClockwise = true;
        }
        
        public int TeethCount { get; }
        public RPM GenerateRpm { get; }
        public Torque GenerateTorque { get; }
        public bool GenerateIsClockwise { get; }
    }
}