using Game.Block.Blocks.Gear;
using Game.Gear.Common;
using Mooresmaster.Model.BlocksModule;
using UniRx;

namespace Game.Block.Blocks.MapObjectMiner
{
    public class GearMapObjectMinerComponent
    {
        private readonly GearEnergyTransformer _gearEnergyTransformer;
        private readonly GearMapObjectMinerProcessorComponent _gearMapObjectMinerProcessorComponent;
        private readonly GearMinerBlockParam _gearMinerBlockParam;
        
        public GearMapObjectMinerComponent(GearEnergyTransformer gearEnergyTransformer, GearMinerBlockParam gearMinerBlockParam)
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
            _gearMapObjectMinerProcessorComponent.SupplyPower(currentElectricPower);
        }
        
        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            IsDestroy = true;
        }
    }
}