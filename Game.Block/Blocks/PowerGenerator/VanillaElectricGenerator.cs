using System.Collections.Generic;
using Game.Block.Config.LoadConfig.Param;
using Game.Block.Event;
using Core.EnergySystem.Electric;
using Core.Item;
using Game.Block.Factory.BlockTemplate;

namespace Game.Block.Blocks.PowerGenerator
{
    public class VanillaElectricGenerator : VanillaPowerGeneratorBase,IElectricGenerator
    {
        public VanillaElectricGenerator(VanillaPowerGeneratorProperties data) : base(data)
        {
        }

        public VanillaElectricGenerator(VanillaPowerGeneratorProperties data,string state) : base(data,state)
        {
        }
    }
}