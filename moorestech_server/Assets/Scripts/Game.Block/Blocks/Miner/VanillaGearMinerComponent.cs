using Game.Block.Blocks.Gear;
using Game.Gear.Common;
using UniRx;

namespace Game.Block.Blocks.Miner
{
    public class VanillaGearMinerComponent
    {
        private readonly GearEnergyTransformer _gearEnergyTransformer;
        private readonly VanillaMinerProcessorComponent _vanillaMinerProcessorComponent;
        //private readonly 
        
        public VanillaGearMinerComponent(VanillaMinerProcessorComponent vanillaMinerProcessorComponent, GearEnergyTransformer gearEnergyTransformer)
        {
            _vanillaMinerProcessorComponent = vanillaMinerProcessorComponent;
            _gearEnergyTransformer = gearEnergyTransformer;
            _gearEnergyTransformer.OnGearUpdate.Subscribe(OnGearUpdate);
        }
        
        private void OnGearUpdate(GearUpdateType gearUpdateType)
        {
            //var requiredRpm = new RPM(_gearMachineBlockParam.RequiredRpm);
            //var requireTorque = new Torque(_gearMachineBlockParam.RequireTorque);
            
            //var currentElectricPower = _gearEnergyTransformer.CalcMachineSupplyPower(requiredRpm, requireTorque);
            //_vanillaMinerProcessorComponent.SupplyPower(currentElectricPower);
        }
        
        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            IsDestroy = true;
        }
    }
}