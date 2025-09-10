using System;
using System.Linq;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.Fluid;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Fluid;
using Game.Gear.Common;
using Mooresmaster.Model.BlocksModule;
using UniRx;

namespace Game.Block.Blocks.Gear
{
    /// <summary>
    ///     歯車の動力で流体を生成するポンプコンポーネント
    /// </summary>
    public class GearPumpComponent : IUpdatableBlockComponent
    {
        private readonly GearEnergyTransformer _gearEnergyTransformer;
        private readonly FluidContainer _fluidContainer;
        private readonly GearPumpBlockParam _pumpParam;
        private readonly IFluidInventory _fluidInventory;
        
        private double _accumulatedTime = 0;
        private double _currentGenerationRate = 0;
        
        public GearPumpComponent(
            GearEnergyTransformer gearEnergyTransformer,
            FluidContainer fluidContainer,
            GearPumpBlockParam pumpParam,
            IFluidInventory fluidInventory)
        {
            _gearEnergyTransformer = gearEnergyTransformer;
            _fluidContainer = fluidContainer;
            _pumpParam = pumpParam;
            _fluidInventory = fluidInventory;
            
            _gearEnergyTransformer.OnGearUpdate.Subscribe(OnGearUpdate);
        }
        
        private void OnGearUpdate(GearUpdateType gearUpdateType)
        {
            var requiredRpm = new RPM(_pumpParam.RequiredRpm);
            var requireTorque = new Torque(_pumpParam.RequireTorque);
            
            // 供給された動力から生成率を計算
            var suppliedPower = _gearEnergyTransformer.CalcMachineSupplyPower(requiredRpm, requireTorque);
            var maxPower = _pumpParam.RequiredRpm * _pumpParam.RequireTorque;
            
            UnityEngine.Debug.Log($"[GearPump] OnGearUpdate - SuppliedPower: {suppliedPower.AsPrimitive()}, MaxPower: {maxPower}, CurrentRPM: {_gearEnergyTransformer.CurrentRpm.AsPrimitive()}, CurrentTorque: {_gearEnergyTransformer.CurrentTorque.AsPrimitive()}");
            
            if (maxPower > 0)
            {
                _currentGenerationRate = suppliedPower.AsPrimitive() / maxPower;
            }
            else
            {
                _currentGenerationRate = 0;
            }
            
            UnityEngine.Debug.Log($"[GearPump] Generation Rate: {_currentGenerationRate}");
        }
        
        public void Update()
        {
            UnityEngine.Debug.Log($"[GearPump] Update - GenerationRate: {_currentGenerationRate}, AccumulatedTime: {_accumulatedTime}");
            
            if (_currentGenerationRate <= 0) return;
            if (_pumpParam.GenerateFluid == null || _pumpParam.GenerateFluid.Length == 0) return;
            
            // 流体生成の処理
            var deltaTime = GameUpdater.UpdateSecondTime;
            _accumulatedTime += deltaTime * _currentGenerationRate;
            
            UnityEngine.Debug.Log($"[GearPump] After time update - AccumulatedTime: {_accumulatedTime}, DeltaTime: {deltaTime}");
            
            foreach (var generateFluid in _pumpParam.GenerateFluid)
            {
                if (generateFluid.GenerateTime <= 0) continue;
                
                // 生成時間に達したら流体を生成
                while (_accumulatedTime >= generateFluid.GenerateTime)
                {
                    _accumulatedTime -= generateFluid.GenerateTime;
                    
                    var fluidId = MasterHolder.FluidMaster.GetFluidId(generateFluid.FluidGuid);
                    var fluidStack = new FluidStack(generateFluid.Amount, fluidId);
                    
                    UnityEngine.Debug.Log($"[GearPump] Generating fluid - Amount: {generateFluid.Amount}, FluidId: {fluidId.AsPrimitive()}, Container current amount: {_fluidContainer.Amount}");
                    
                    // 内部タンクに流体を追加
                    var remain = _fluidContainer.AddLiquid(fluidStack, FluidContainer.Empty);
                    
                    UnityEngine.Debug.Log($"[GearPump] After adding - Container amount: {_fluidContainer.Amount}, Remaining: {remain.Amount}");
                    
                    // タンクが満杯の場合は生成を停止
                    if (remain.Amount > 0)
                    {
                        _accumulatedTime = 0;
                        break;
                    }
                }
            }
        }
        
        public bool IsDestroy { get; private set; }
        
        public void Destroy()
        {
            IsDestroy = true;
        }
    }
}