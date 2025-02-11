using Game.Block.Blocks.Gear;
using Game.Block.Interface.Component;
using Game.Gear.Common;
using Mooresmaster.Model.BlocksModule;
using UniRx;

namespace Game.Block.Blocks.MapObjectMiner
{
    public class VanillaGearMapObjectMinerComponent : IBlockComponent
    {
        private readonly GearEnergyTransformer _gearEnergyTransformer;
        private readonly VanillaGearMapObjectMinerProcessorComponent _vanillaGearMapObjectMinerProcessorComponent;
        private readonly GearMapObjectMinerBlockParam _gearMinerBlockParam;
        
        public VanillaGearMapObjectMinerComponent(GearEnergyTransformer gearEnergyTransformer, GearMapObjectMinerBlockParam gearMinerBlockParam)
        {
            _gearMinerBlockParam = gearMinerBlockParam;
            _gearEnergyTransformer = gearEnergyTransformer;
            _gearEnergyTransformer.OnGearUpdate.Subscribe(OnGearUpdate);
        }
        
        private void OnGearUpdate(GearUpdateType gearUpdateType)
        {
            var requiredRpm = new RPM(_gearMinerBlockParam.RequiredRpm);
            var requireTorque = new Torque(_gearMinerBlockParam.RequireTorque);
            
            var currentElectricPower = _gearEnergyTransformer.CalcMachineSupplyPower(requiredRpm, requireTorque);
            _vanillaGearMapObjectMinerProcessorComponent.SupplyPower(currentElectricPower);
        }
        
        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            IsDestroy = true;
        }
    }
}