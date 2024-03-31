using Core.EnergySystem.Gear;
using Game.Block.Factory.BlockTemplate;

namespace Game.Block.Blocks.PowerGenerator
{
    public class VanillaGearGenerator : VanillaPowerGeneratorBase, IGearGenerator
    {
        public VanillaGearGenerator(VanillaPowerGeneratorProperties data) : base(data)
        {
        }

        public VanillaGearGenerator(VanillaPowerGeneratorProperties data, string state) : base(data, state)
        {
        }
    }
}