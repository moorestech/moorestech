using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Gear.Common;
using Mooresmaster.Model.BlocksModule;

namespace Game.Block.Blocks.Gear
{
    public class SimpleGearGeneratorComponent : GearEnergyTransformer, IGearGenerator
    {
        public int TeethCount { get; }
        public RPM GenerateRpm { get; private set; }
        public Torque GenerateTorque { get; private set; }
        public bool GenerateIsClockwise { get; }
        
        public SimpleGearGeneratorComponent(SimpleGearGeneratorBlockParam simpleGearGeneratorBlockParam, BlockInstanceId blockInstanceId, IBlockConnectorComponent<IGearEnergyTransformer> connectorComponent) :
            base(null, blockInstanceId, connectorComponent)
        {
            TeethCount = simpleGearGeneratorBlockParam.TeethCount;
            GenerateRpm = new RPM(simpleGearGeneratorBlockParam.GenerateRpm);
            GenerateTorque = new Torque(simpleGearGeneratorBlockParam.GenerateTorque);
            GenerateIsClockwise = true;
        }

        public void SetGenerateRpmForDebug(float rpm)
        {
            GenerateRpm = new RPM(rpm);
            GearNetworkDatastore.GetGearNetwork(BlockInstanceId).MarkGeneratorOutputDirty();
        }

        public void SetGenerateTorqueForDebug(float torque)
        {
            GenerateTorque = new Torque(torque);
            GearNetworkDatastore.GetGearNetwork(BlockInstanceId).MarkGeneratorOutputDirty();
        }
    }
}
