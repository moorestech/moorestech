using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Core.Update;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Context;
using Game.EnergySystem;
using Game.Gear.Common;
using Mooresmaster.Model.BlockConnectInfoModule;
using Mooresmaster.Model.BlocksModule;
using UniRx;
using UnityEngine;

namespace Game.Block.Blocks.Gear
{
    public class GearEnergyTransformer : IGearEnergyTransformer, IBlockStateObservable
    {
        public IObservable<Unit> OnChangeBlockState => _simpleGearService.BlockStateChange;
        public IObservable<GearUpdateType> OnGearUpdate => _simpleGearService.OnGearUpdate;
        
        public BlockInstanceId BlockInstanceId { get; }
        public RPM CurrentRpm => _simpleGearService.CurrentRpm;
        public Torque CurrentTorque => _simpleGearService.CurrentTorque;
        public bool IsCurrentClockwise => _simpleGearService.IsCurrentClockwise;

        public bool IsDestroy { get; private set; }
        
        private readonly IBlockConnectorComponent<IGearEnergyTransformer> _connectorComponent;
        
        protected readonly Torque RequiredTorque;
        private readonly SimpleGearService _simpleGearService;

        private readonly IBlockRemover _blockRemover;
        private readonly Guid _blockGuid;
        private IDisposable _updateSubscription;
        private float _accumulatedTime;
        private int _maxRpm;
        private float _maxTorque;
        private float _overloadCheckIntervalSeconds;
        private float _baseBreakageProbability;
        
        public GearEnergyTransformer(
            Torque requiredTorque, 
            BlockInstanceId blockInstanceId, 
            IBlockConnectorComponent<IGearEnergyTransformer> connectorComponent,
            IBlockRemover blockRemover,
            Guid blockGuid)
        {
            RequiredTorque = requiredTorque;
            BlockInstanceId = blockInstanceId;
            _connectorComponent = connectorComponent;
            _blockRemover = blockRemover;
            _blockGuid = blockGuid;
            _simpleGearService = new SimpleGearService(blockInstanceId);
            
            GearNetworkDatastore.AddGear(this);
            InitializeOverloadMonitoring();
        }
        
        public BlockStateDetail[] GetBlockStateDetails()
        {
            return new []{ _simpleGearService.GetBlockStateDetail() };
        }
        
        public Torque GetRequiredTorque(RPM rpm, bool isClockwise)
        {
            return RequiredTorque;
        }
        
        public void StopNetwork()
        {
            _simpleGearService.StopNetwork();
        }
        
        public virtual void SupplyPower(RPM rpm, Torque torque, bool isClockwise)
        {
            _simpleGearService.SupplyPower(rpm, torque, isClockwise);
        }
        
        public List<GearConnect> GetGearConnects()
        {
            var result = new List<GearConnect>();
            foreach (var target in _connectorComponent.ConnectedTargets)
            {
                result.Add(new GearConnect(target.Key, (GearConnectOption)target.Value.SelfOption, (GearConnectOption)target.Value.TargetOption));
            }
            return result;
        }
        
        public void Destroy()
        {
            _updateSubscription?.Dispose();
            IsDestroy = true;
            GearNetworkDatastore.RemoveGear(this);
            _simpleGearService.Destroy();
        }

        private void InitializeOverloadMonitoring()
        {
            LoadOverloadParameters();

            if (_maxRpm > 0 && _maxTorque > 0 && _baseBreakageProbability > 0)
            {
                _updateSubscription = GameUpdater.UpdateObservable.Subscribe(_ => OnUpdate());
            }
        }

        #region Internal

        void LoadOverloadParameters()
        {
            // Check if BlockMaster is initialized. In tests it might not be fully set up.
            if (MasterHolder.BlockMaster == null) return;

            var blockData = MasterHolder.BlockMaster.GetBlockMaster(_blockGuid);
            var overloadParam = blockData.BlockParam as IGearOverloadParam;

            if (overloadParam != null)
            {
                _maxRpm = overloadParam.MaxRpm;
                _maxTorque = overloadParam.MaxTorque;
                _overloadCheckIntervalSeconds = overloadParam.OverloadCheckIntervalSeconds;
                _baseBreakageProbability = overloadParam.BaseBreakageProbability;

                if (_overloadCheckIntervalSeconds <= 0)
                {
                    _overloadCheckIntervalSeconds = 1.0f;
                }
            }
            else
            {
                _maxRpm = 0;
                _maxTorque = 0;
            }
        }

        void OnUpdate()
        {
            _accumulatedTime += (float)GameUpdater.UpdateSecondTime;

            if (_accumulatedTime < _overloadCheckIntervalSeconds)
            {
                return;
            }

            _accumulatedTime = 0;

            var gearNetwork = GearNetworkDatastore.GetGearNetwork(BlockInstanceId);
            if (gearNetwork == null)
            {
                return;
            }

            var networkInfo = gearNetwork.CurrentGearNetworkInfo;
            var currentRpm = CurrentRpm.AsPrimitive();
            var currentTorque = CurrentTorque.AsPrimitive();

            var rpmExceeded = currentRpm > _maxRpm;
            var torqueExceeded = currentTorque > _maxTorque;

            if (!rpmExceeded && !torqueExceeded)
            {
                return;
            }

            var breakageProbability = CalculateBreakageProbability(currentRpm, currentTorque);
            var randomValue = UnityEngine.Random.Range(0f, 1f);

            if (randomValue <= breakageProbability)
            {
                var blockPositionInfo = GetBlockPositionInfo();
                if (blockPositionInfo != null)
                {
                    _blockRemover.RemoveBlock(blockPositionInfo, BlockRemovalType.Broken);
                    _updateSubscription?.Dispose();
                }
            }
        }

        float CalculateBreakageProbability(float currentRpm, float currentTorque)
        {
            var rpmMultiplier = 1.0f;
            var torqueMultiplier = 1.0f;

            if (currentRpm > _maxRpm && _maxRpm > 0)
            {
                rpmMultiplier = currentRpm / _maxRpm;
            }

            if (currentTorque > _maxTorque && _maxTorque > 0)
            {
                torqueMultiplier = currentTorque / _maxTorque;
            }

            var finalProbability = _baseBreakageProbability * rpmMultiplier * torqueMultiplier;
            return Mathf.Clamp01(finalProbability);
        }

        BlockPositionInfo GetBlockPositionInfo()
        {
            var block = ServerContext.WorldBlockDatastore.GetBlock(BlockInstanceId);
            return block?.BlockPositionInfo;
        }

        #endregion
    }
    
    public static class GearEnergyTransformerExtension
    {
        public static ElectricPower CalcMachineSupplyPower(this GearEnergyTransformer energyTransformer, RPM requiredRpm, Torque requiredTorque)
        {
            var currentRpm = energyTransformer.CurrentRpm;
            var currentTorque = energyTransformer.CurrentTorque;
            
            var rpmRate = Mathf.Min((currentRpm / requiredRpm).AsPrimitive(), 1);
            var torqueRate = Mathf.Min((currentTorque / requiredTorque).AsPrimitive(), 1);
            var powerRate = rpmRate * torqueRate;
            
            var requiredGearPower = requiredRpm.AsPrimitive() * requiredTorque.AsPrimitive();
            var currentElectricPower = new ElectricPower(requiredGearPower * powerRate);
            
            return currentElectricPower;
        }
    }
}
