using Game.Block.Blocks.Gear;
using Game.Block.Interface.Component;
using Game.Gear.Common;
using Mooresmaster.Model.BlocksModule;
using UniRx;
using UnityEngine;

namespace Game.Block.Blocks.Miner
{
    public class VanillaGearMinerComponent : IBlockComponent
    {
        private readonly GearEnergyTransformer _gearEnergyTransformer;
        private readonly VanillaMinerProcessorComponent _vanillaMinerProcessorComponent;
        private readonly GearMinerBlockParam _gearMinerBlockParam;
        
        public VanillaGearMinerComponent(VanillaMinerProcessorComponent vanillaMinerProcessorComponent, GearEnergyTransformer gearEnergyTransformer, GearMinerBlockParam gearMinerBlockParam)
        {
            _gearMinerBlockParam = gearMinerBlockParam;
            _vanillaMinerProcessorComponent = vanillaMinerProcessorComponent;
            _gearEnergyTransformer = gearEnergyTransformer;
            _gearEnergyTransformer.OnGearUpdate.Subscribe(OnGearUpdate);
        }
        
        private void OnGearUpdate(GearUpdateType gearUpdateType)
        {
            var requiredRpm = new RPM(_gearMinerBlockParam.RequiredRpm);
            var requireTorque = new Torque(_gearMinerBlockParam.RequireTorque);
            
            var currentElectricPower = _gearEnergyTransformer.CalcMachineSupplyPower(requiredRpm, requireTorque);
            _vanillaMinerProcessorComponent.SupplyPower(currentElectricPower);
        }
        
        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            IsDestroy = true;
        }
    }
}