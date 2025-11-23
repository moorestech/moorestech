using System;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Core.Update;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.State;
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
        private readonly IBlockRemover _blockRemover;
        private readonly Guid _blockGuid;
        
        protected readonly Torque RequiredTorque;
        private readonly SimpleGearService _simpleGearService;
        private IDisposable _updateSubscription;
        private float _maxRpm;
        private float _maxTorque;
        private float _overloadCheckIntervalSeconds;
        private float _baseBreakageProbability;
        private float _accumulatedTime;
        
        public GearEnergyTransformer(Torque requiredTorque, BlockInstanceId blockInstanceId, IBlockConnectorComponent<IGearEnergyTransformer> connectorComponent, IBlockRemover blockRemover, Guid blockGuid)
        {
            // 主要フィールドを初期化する
            // Initialize primary fields
            RequiredTorque = requiredTorque;
            BlockInstanceId = blockInstanceId;
            _connectorComponent = connectorComponent;
            _blockRemover = blockRemover;
            _blockGuid = blockGuid;
            _simpleGearService = new SimpleGearService(blockInstanceId);
            
            LoadOverloadParameters();
            InitializeOverloadMonitoring();
            
            GearNetworkDatastore.AddGear(this);
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
            IsDestroy = true;
            _updateSubscription?.Dispose();
            GearNetworkDatastore.RemoveGear(this);
            _simpleGearService.Destroy();
        }
        
        private void LoadOverloadParameters()
        {
            // マスターデータから過負荷パラメータを取得する
            // Load overload parameters from master data
            var blockData = MasterHolder.BlockMaster.GetBlockMaster(_blockGuid);
            var overloadParam = blockData.BlockParam as IGearOverloadParam;

            if (overloadParam == null)
            {
                _maxRpm = 0;
                _maxTorque = 0;
                _overloadCheckIntervalSeconds = 1f;
                _baseBreakageProbability = 0f;
                return;
            }

            _maxRpm = overloadParam.MaxRpm;
            _maxTorque = overloadParam.MaxTorque;
            _overloadCheckIntervalSeconds = overloadParam.OverloadCheckIntervalSeconds <= 0 ? 1f : overloadParam.OverloadCheckIntervalSeconds;
            _baseBreakageProbability = Mathf.Clamp01(overloadParam.BaseBreakageProbability);
        }

        private void InitializeOverloadMonitoring()
        {
            // 過負荷チェックが有効な場合のみ購読を開始する
            // Subscribe only when overload checks are enabled
            var enabled = _maxRpm > 0f && _maxTorque > 0f && _baseBreakageProbability > 0f;
            if (!enabled) return;

            _updateSubscription = GameUpdater.UpdateObservable.Subscribe(_ => OnUpdate());

            #region Internal

            void OnUpdate()
            {
                _accumulatedTime += (float)GameUpdater.UpdateSecondTime;
                if (_accumulatedTime < _overloadCheckIntervalSeconds) return;

                _accumulatedTime = 0f;

                var gearNetwork = GearNetworkDatastore.GetGearNetwork(BlockInstanceId);
                if (gearNetwork == null) return;

                var currentRpm = CurrentRpm.AsPrimitive();
                var currentTorque = CurrentTorque.AsPrimitive();
                var rpmExceeded = currentRpm > _maxRpm;
                var torqueExceeded = currentTorque > _maxTorque;

                if (!rpmExceeded && !torqueExceeded) return;

                var breakageProbability = CalculateBreakageProbability(currentRpm, currentTorque, rpmExceeded, torqueExceeded);
                var randomValue = UnityEngine.Random.Range(0f, 1f);

                if (randomValue > breakageProbability) return;

                var block = ServerContext.WorldBlockDatastore.GetBlock(BlockInstanceId);
                if (block == null) return;

                _blockRemover.RemoveBlock(block.BlockPositionInfo, BlockRemovalType.Broken);
                _updateSubscription?.Dispose();
            }

            float CalculateBreakageProbability(float currentRpm, float currentTorque, bool rpmExceeded, bool torqueExceeded)
            {
                // 超過倍率に応じて破壊確率を計算する
                // Calculate breakage probability based on exceed multipliers
                var rpmMultiplier = rpmExceeded && _maxRpm > 0 ? currentRpm / _maxRpm : 1f;
                var torqueMultiplier = torqueExceeded && _maxTorque > 0 ? currentTorque / _maxTorque : 1f;

                var probability = _baseBreakageProbability * rpmMultiplier * torqueMultiplier;
                return Mathf.Clamp01(probability);
            }

            #endregion
        }
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
