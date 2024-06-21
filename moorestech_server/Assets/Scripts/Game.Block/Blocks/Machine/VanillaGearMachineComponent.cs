using System.Collections.Generic;
using System.Linq;
using Game.Block.Blocks.Gear;
using Game.Block.Config.LoadConfig.Param;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.EnergySystem;
using Game.Gear.Common;
using UnityEngine;

namespace Game.Block.Blocks.Machine
{
    /// <summary>
    ///     歯車機械を表すクラス
    /// </summary>
    public class VanillaGearMachineComponent : IGear
    {
        private readonly GearMachineConfigParam _configParam;
        private readonly IBlockConnectorComponent<IGearEnergyTransformer> _connectorComponent;
        
        private readonly SimpleGearService _simpleGearService;
        private readonly VanillaMachineProcessorComponent _vanillaMachineProcessorComponent;
        public VanillaGearMachineComponent(GearMachineConfigParam configParam, VanillaMachineProcessorComponent vanillaMachineProcessorComponent, IBlockConnectorComponent<IGearEnergyTransformer> connectorComponent, BlockInstanceId blockInstanceId)
        {
            BlockInstanceId = blockInstanceId;
            _configParam = configParam;
            _vanillaMachineProcessorComponent = vanillaMachineProcessorComponent;
            _connectorComponent = connectorComponent;
            _simpleGearService = new SimpleGearService();
        }
        public Torque CurrentTorque => _simpleGearService.CurrentTorque;
        public int TeethCount => _configParam.TeethCount;
        public BlockInstanceId BlockInstanceId { get; }
        public RPM CurrentRpm => _simpleGearService.CurrentRpm;
        public bool IsCurrentClockwise => _simpleGearService.IsCurrentClockwise;
        public bool IsRocked => _simpleGearService.IsRocked;
        
        public IReadOnlyList<GearConnect> Connects =>
            _connectorComponent.ConnectedTargets.Select(
                target => new GearConnect(target.Key, (GearConnectOption)target.Value.selfOption, (GearConnectOption)target.Value.targetOption)
            ).ToArray();
        
        public Torque GetRequiredTorque(RPM rpm, bool isClockwise)
        {
            BlockException.CheckDestroy(this);
            return _vanillaMachineProcessorComponent.CurrentState is ProcessState.Processing ? _configParam.RequiredTorque : new Torque(0);
        }
        
        public void Rocked()
        {
            BlockException.CheckDestroy(this);
            _simpleGearService.Rocked();
            _vanillaMachineProcessorComponent.SupplyPower(new ElectricPower(0));
        }
        
        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            BlockException.CheckDestroy(this);
            IsDestroy = true;
        }
        
        public void SupplyPower(RPM rpm, Torque torque, bool isClockwise)
        {
            BlockException.CheckDestroy(this);
            _simpleGearService.SupplyPower(rpm, torque, isClockwise);
            
            var rpmRate = Mathf.Min((rpm / _configParam.RequiredRpm).AsPrimitive(), 1);
            var torqueRate = Mathf.Min((torque / _configParam.RequiredTorque).AsPrimitive(), 1);
            var powerRate = rpmRate * torqueRate;
            _vanillaMachineProcessorComponent.SupplyPower(_configParam.RequiredPower * powerRate);
        }
    }
}