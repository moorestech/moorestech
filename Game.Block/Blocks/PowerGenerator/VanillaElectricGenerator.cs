using System.Collections.Generic;
using Core.Block.BlockFactory.BlockTemplate;
using Core.Block.Config.LoadConfig.Param;
using Core.Block.Event;
using Core.EnergySystem.Electric;
using Core.Item;

namespace Core.Block.Blocks.PowerGenerator
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