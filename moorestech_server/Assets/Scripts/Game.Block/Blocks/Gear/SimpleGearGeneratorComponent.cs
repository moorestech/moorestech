using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Gear.Common;
using Mooresmaster.Model.BlocksModule;

namespace Game.Block.Blocks.Gear
{
    public class SimpleGearGeneratorComponent : GearEnergyTransformer, IGearGenerator
    {
        public int TeethCount { get; }
        public RPM GenerateRpm { get; }
        public Torque GenerateTorque { get; }
        public bool GenerateIsClockwise { get; }
        
        public SimpleGearGeneratorComponent(SimpleGearGeneratorBlockParam simpleGearGeneratorBlockParam, BlockInstanceId blockInstanceId, IBlockConnectorComponent<IGearEnergyTransformer> connectorComponent) :
            base(new Torque(0), blockInstanceId, connectorComponent)
        {
            TeethCount = simpleGearGeneratorBlockParam.TeethCount;
            GenerateRpm = new RPM(simpleGearGeneratorBlockParam.GenerateTorque);
            GenerateTorque = new Torque(simpleGearGeneratorBlockParam.GenerateTorque);
            GenerateIsClockwise = true;
        }
    }
}