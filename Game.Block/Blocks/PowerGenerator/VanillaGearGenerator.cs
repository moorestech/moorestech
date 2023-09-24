using System.Collections.Generic;
using Game.Block.Config.LoadConfig.Param;
using Game.Block.Event;
using Core.EnergySystem.Gear;
using Core.Item;
using Game.Block.Factory.BlockTemplate;

namespace Game.Block.Blocks.PowerGenerator
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