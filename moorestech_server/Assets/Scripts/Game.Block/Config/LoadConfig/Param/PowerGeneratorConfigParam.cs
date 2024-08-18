using System.Collections.Generic;
using Core.Item.Interface.Config;
using Game.Block.Interface.BlockConfig;
using Game.EnergySystem;

namespace Game.Block.Config.LoadConfig.Param
{
    public class PowerGeneratorConfigParam : IBlockConfigParam
    {
        public readonly Dictionary<int, FuelSetting> FuelSettings;
        public readonly int FuelSlot;
        public readonly ElectricPower InfinityPower;
        public readonly bool IsInfinityPower;
        
        private PowerGeneratorConfigParam(Dictionary<int, FuelSetting> fuelSettings, int fuelSlot, bool isInfinityPower,
            ElectricPower infinityPower)
        {
            FuelSettings = fuelSettings;
            FuelSlot = fuelSlot;
            IsInfinityPower = isInfinityPower;
            InfinityPower = infinityPower;
        }
        
        public static IBlockConfigParam Generate(dynamic blockParam, IItemConfig itemConfig)
        {
            var fuelSettings = new Dictionary<int, FuelSetting>();
            foreach (var fuel in blockParam.fuel)
            {
                // TODO modパースのエラー
                
                string itemModId = fuel.itemModId;
                string idItemName = fuel.itemName;
                int time = fuel.time;
                int power = fuel.power;
                
                var itemId = itemConfig.GetItemId(itemModId, idItemName);
                
                fuelSettings.Add(itemId, new FuelSetting(itemId, time, new ElectricPower(power)));
            }
            
            int fuelSlot = blockParam.fuelSlot;
            bool isInfinityPower = blockParam.isInfinityPower;
            int infinityPower = blockParam.infinityPower;
            
            return new PowerGeneratorConfigParam(fuelSettings, fuelSlot, isInfinityPower, new ElectricPower(infinityPower));
        }
    }
    
    public class FuelSetting
    {
        public readonly int ItemId;
        public readonly ElectricPower Power;
        public readonly int Time;
        
        public FuelSetting(int itemId, int time, ElectricPower power)
        {
            ItemId = itemId;
            Time = time;
            Power = power;
        }
    }
}