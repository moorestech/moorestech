using System.Collections.Generic;
using Core.Block.BlockFactory.BlockTemplate;
using Core.Block.Config.LoadConfig.Param;
using Core.Block.Event;
using Core.EnergySystem.Gear;
using Core.Item;

namespace Core.Block.Blocks.PowerGenerator
{
    public class VanillaGearGenerator : VanillaPowerGeneratorBase,IGearGenerator
    {
        public VanillaGearGenerator(VanillaPowerGeneratorProperties data) : base(data)
        {
        }

        public VanillaGearGenerator(VanillaPowerGeneratorProperties data,string state) : base(data,state)
        {
        }
    }
}