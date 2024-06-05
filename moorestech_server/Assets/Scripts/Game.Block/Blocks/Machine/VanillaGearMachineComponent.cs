using Game.Block.Blocks.Gear;
using Game.Block.Config.LoadConfig.Param;
using Game.Block.Interface.Component;
using Game.Gear.Common;
using UnityEngine;

namespace Game.Block.Blocks.Machine
{
    /// <summary>
    ///     歯車機械を表すクラス
    /// </summary>
    public class VanillaGearMachineComponent : GearComponent
    {
        public override float RequiredPower => _vanillaMachineProcessorComponent.CurrentState is ProcessState.Processing ? _configParam.RequiredPower : 0;
        
        private readonly VanillaMachineProcessorComponent _vanillaMachineProcessorComponent;
        private readonly GearMachineConfigParam _configParam;
        
        public VanillaGearMachineComponent(GearMachineConfigParam configParam, int entityId, IBlockConnectorComponent<IGearEnergyTransformer> connectorComponent, VanillaMachineProcessorComponent processor) : 
            base(configParam.TeethCount, configParam.RequiredPower, entityId, connectorComponent)
        {
            _configParam = configParam;
            _vanillaMachineProcessorComponent = processor;
        }
        
        public override void SupplyPower(float rpm, float torque, bool isClockwise)
        {
            base.SupplyPower(rpm, torque, isClockwise);
            
            var rpmRate = Mathf.Min(rpm / _configParam.RequiredRpm, 1);
            var torqueRate = Mathf.Min(torque / _configParam.RequiredTorque, 1);
            var powerRate = rpmRate * torqueRate;
            
            _vanillaMachineProcessorComponent.SupplyPower((int)(_configParam.RequiredPower * powerRate));
        }
    }
}