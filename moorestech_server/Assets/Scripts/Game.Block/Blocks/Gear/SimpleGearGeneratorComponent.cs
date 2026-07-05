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

        // 定数出力のため毎tick処理は不要
        // Constant output, so no per-tick processing is required
        public bool RequiresContinuousTick => false;

        public void ConsumeGeneratorTick(float networkLoadRate)
        {
        }

        public SimpleGearGeneratorComponent(SimpleGearGeneratorBlockParam simpleGearGeneratorBlockParam, BlockInstanceId blockInstanceId, IBlockConnectorComponent<IGearEnergyTransformer> connectorComponent) :
            base(null, blockInstanceId, connectorComponent)
        {
            TeethCount = simpleGearGeneratorBlockParam.TeethCount;
            GenerateRpm = new RPM(simpleGearGeneratorBlockParam.GenerateRpm);
            GenerateTorque = new Torque(simpleGearGeneratorBlockParam.GenerateTorque);
            GenerateIsClockwise = true;
        }
    }
}