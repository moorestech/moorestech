using System.Collections.Generic;
using System.Linq;
using Game.Block.Blocks.Gear;
using Game.Block.Config.LoadConfig.Param;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Gear.Common;
using UnityEngine;

namespace Game.Block.Blocks.Machine
{
    /// <summary>
    ///     歯車機械を表すクラス
    /// </summary>
    public class VanillaGearMachineComponent : IGear
    {
        public int TeethCount => _configParam.TeethCount;
        public BlockInstanceId BlockInstanceId { get; }
        public float CurrentRpm => _simpleGearService.CurrentRpm;
        public float CurrentTorque => _simpleGearService.CurrentTorque;
        public bool IsCurrentClockwise => _simpleGearService.IsCurrentClockwise;
        public bool IsRocked => _simpleGearService.IsRocked;
        
        public IReadOnlyList<GearConnect> Connects =>
            _connectorComponent.ConnectTargets.Select(
                target => new GearConnect(target.Key, (GearConnectOption)target.Value.selfOption, (GearConnectOption)target.Value.targetOption)
            ).ToArray();
        
        private readonly SimpleGearService _simpleGearService;
        private readonly VanillaMachineProcessorComponent _vanillaMachineProcessorComponent;
        private readonly IBlockConnectorComponent<IGearEnergyTransformer> _connectorComponent;
        private readonly GearMachineConfigParam _configParam;
        public VanillaGearMachineComponent(GearMachineConfigParam configParam, VanillaMachineProcessorComponent vanillaMachineProcessorComponent, IBlockConnectorComponent<IGearEnergyTransformer> connectorComponent, BlockInstanceId blockInstanceId)
        {
            BlockInstanceId = blockInstanceId;
            _configParam = configParam;
            _vanillaMachineProcessorComponent = vanillaMachineProcessorComponent;
            _connectorComponent = connectorComponent;
            _simpleGearService = new SimpleGearService();
        }
        
        public float GetRequiredTorque(float rpm, bool isClockwise)
        {
            BlockException.CheckDestroy(this);
            return _vanillaMachineProcessorComponent.CurrentState is ProcessState.Processing ? 
                _configParam.RequiredTorque : 0;
        }
        
        public void Rocked()
        {
            BlockException.CheckDestroy(this);
            _simpleGearService.Rocked();
            _vanillaMachineProcessorComponent.SupplyPower(0);
        }
        
        public void SupplyPower(float rpm, float torque, bool isClockwise)
        {
            BlockException.CheckDestroy(this);
            _simpleGearService.SupplyPower(rpm, torque, isClockwise);
            
            var rpmRate = Mathf.Min(rpm / _configParam.RequiredRpm, 1);
            var torqueRate = Mathf.Min(torque / _configParam.RequiredTorque, 1);
            var powerRate = rpmRate * torqueRate;
            _vanillaMachineProcessorComponent.SupplyPower((int)(_configParam.RequiredPower * powerRate));
        }
        
        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            BlockException.CheckDestroy(this);
            IsDestroy = true;
        }
    }
}