using System.Collections.Generic;
using Core.Block.Config.LoadConfig.Param;

namespace Core.Block.Config.LoadConfig.ConfigParamGenerator
{
    public class PowerGeneratorConfigParamGenerator : IBlockConfigParamGenerator
    {
        public BlockConfigParamBase Generate(dynamic blockParam)
        {
            var fuelSettings = new List<FuelSetting>();
            foreach (var fuel in blockParam.fuel)
            {
                int id = fuel.id;
                int time = fuel.time;
                int power = fuel.power;
                fuelSettings.Add(new FuelSetting(id,time,power));
            }
            
            int fuelSlot = blockParam.fuelSlot;
            
            return new PowerGeneratorConfigParam(fuelSettings,fuelSlot);
        }
    }
}