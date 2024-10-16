using Game.Block.Blocks.Gear;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.EnergySystem;
using Game.Gear.Common;
using Mooresmaster.Model.BlocksModule;
using UniRx;
using UnityEngine;

namespace Game.Block.Blocks.Machine
{
    /// <summary>
    ///     歯車機械を表すクラス
    /// </summary>
    public class VanillaGearMachineComponent : IBlockComponent
    {
        private readonly GearEnergyTransformer _gearEnergyTransformer;
        private readonly VanillaMachineProcessorComponent _vanillaMachineProcessorComponent;
        private readonly GearMachineBlockParam _gearMachineBlockParam;
        
        public VanillaGearMachineComponent(VanillaMachineProcessorComponent vanillaMachineProcessorComponent, GearEnergyTransformer gearEnergyTransformer, GearMachineBlockParam gearMachineBlockParam)
        {
            _vanillaMachineProcessorComponent = vanillaMachineProcessorComponent;
            _gearEnergyTransformer = gearEnergyTransformer;
            _gearMachineBlockParam = gearMachineBlockParam;
            
            _gearEnergyTransformer.OnGearUpdate.Subscribe(OnGearUpdate);
        }
        
        private void OnGearUpdate(GearUpdateType gearUpdateType)
        {
            var requiredRpm = new RPM(_gearMachineBlockParam.RequiredRpm);
            var requireTorque = new Torque(_gearMachineBlockParam.RequireTorque);
            
            var currentElectricPower = _gearEnergyTransformer.CalcMachineSupplyPower(requiredRpm, requireTorque);
            _vanillaMachineProcessorComponent.SupplyPower(currentElectricPower);
        }
        
        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            BlockException.CheckDestroy(this);
            IsDestroy = true;
        }
    }
}