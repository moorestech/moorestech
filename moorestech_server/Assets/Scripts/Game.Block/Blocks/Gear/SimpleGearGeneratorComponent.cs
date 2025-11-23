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
        
        public SimpleGearGeneratorComponent(SimpleGearGeneratorBlockParam param, IBlockRemover remover, BlockInstanceId blockInstanceId, IBlockConnectorComponent<IGearEnergyTransformer> connectorComponent) :
            base(new Torque(0), GearOverloadConfig.Create(param.Gear), remover, blockInstanceId, connectorComponent)
        {
            TeethCount = param.TeethCount;
            GenerateRpm = new RPM(param.GenerateRpm);
            GenerateTorque = new Torque(param.GenerateTorque);
            GenerateIsClockwise = true;
        }
    }
}
