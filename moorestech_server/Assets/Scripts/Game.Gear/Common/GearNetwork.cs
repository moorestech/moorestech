using System;
using System.Collections.Generic;
using Game.Block.Interface;
using UnityEngine;

namespace Game.Gear.Common
{
    public class GearNetwork
    {
        private readonly Dictionary<BlockInstanceId, GearRotationInfo> _checkedGearComponents = new();
        private readonly List<IGearGenerator> _gearGenerators = new();
        private readonly List<IGearEnergyTransformer> _gearTransformers = new();
        public readonly GearNetworkId NetworkId;
        
        public GearNetwork(GearNetworkId networkId)
        {
            NetworkId = networkId;
        }
        
        public IReadOnlyList<IGearEnergyTransformer> GearTransformers => _gearTransformers;
        
        public IReadOnlyList<IGearGenerator> GearGenerators => _gearGenerators;
        
        public void AddGear(IGearEnergyTransformer gear)
        {
            switch (gear)
            {
                case IGearGenerator generator:
                    _gearGenerators.Add(generator);
                    break;
                default:
                    _gearTransformers.Add(gear);
                    break;
            }
        }
        
        public void RemoveGear(IGearEnergyTransformer gear)
        {
            switch (gear)
            {
                case IGearGenerator generator:
                    _gearGenerators.Remove(generator);
                    break;
                default:
                    _gearTransformers.Remove(gear);
                    break;
            }
        }
        
        public void ManualUpdate()
        {
            //もっとも早いジェネレーターを選定し、そのジェネレーターを起点として、各歯車コンポーネントのRPMと回転方向を計算していく
            IGearGenerator fastestOriginGenerator = null;
            foreach (var gearGenerator in GearGenerators)
            {
                if (fastestOriginGenerator == null)
                {
                    fastestOriginGenerator = gearGenerator;
                    continue;
                }
                
                if (gearGenerator.GenerateRpm > fastestOriginGenerator.GenerateRpm) fastestOriginGenerator = gearGenerator;
            }
            
            if (fastestOriginGenerator == null)
            {
                //ジェネレーターがない場合はすべてにゼロを供給して終了
                foreach (var transformer in GearTransformers) transformer.SupplyPower(new RPM(0), new Torque(0), true);
                return;
            }
            
            //そのジェネレータと接続している各歯車コンポーネントを深さ優先度探索でたどり、RPMと回転方向を計算していく
            _checkedGearComponents.Clear();
            var generatorGearRotationInfo = new GearRotationInfo(fastestOriginGenerator.GenerateRpm, fastestOriginGenerator.GenerateIsClockwise, fastestOriginGenerator);
            _checkedGearComponents.Add(fastestOriginGenerator.BlockInstanceId, generatorGearRotationInfo);
            var rocked = false;
            foreach (var connect in fastestOriginGenerator.GetGearConnects())
            {
                rocked = CalcGearInfo(connect, generatorGearRotationInfo);
                //ロックを検知したので処理を終了
                if (rocked) break;
            }
            
            if (rocked)
            {
                SetRocked();
                return;
            }
            
            //すべてのジェネレーターから生成GPを取得し、合算する
            DistributeGearPower();
            
            #region Internal
            
            bool CalcGearInfo(GearConnect gearConnect, GearRotationInfo connectGearRotationInfo)
            {
                var transformer = gearConnect.Transformer;
                
                //RPMと回転方向を計算する
                var isReverseRotation = IsReverseRotation(gearConnect);
                var isClockwise = isReverseRotation ? !connectGearRotationInfo.IsClockwise : connectGearRotationInfo.IsClockwise;
                RPM rpm;
                if (transformer is IGear gear &&
                    connectGearRotationInfo.EnergyTransformer is IGear connectGear &&
                    isReverseRotation)
                {
                    var gearRate = (float)connectGear.TeethCount / gear.TeethCount;
                    rpm = connectGearRotationInfo.Rpm * gearRate;
                }
                else
                {
                    rpm = connectGearRotationInfo.Rpm;
                }
                
                // もし既に計算済みの場合、新たな計算と一致するかを計算し、一致しない場合はロックフラグを立てる
                if (_checkedGearComponents.TryGetValue(transformer.BlockInstanceId, out var info))
                {
                    if (info.IsClockwise != isClockwise || // 回転方向が一致しない場合
                        Math.Abs((info.Rpm - rpm).AsPrimitive()) > 0.1f) // RPMが一致しない場合
                        return true;
                    
                    // 深さ優先度探索でループになったのでこの探索は終了
                    return false;
                }
                
                if (transformer is IGearGenerator generator
                    && generator.GenerateIsClockwise != isClockwise // もしこれがジェネレーターである場合、回転方向が合っているかを確認
                    && fastestOriginGenerator.BlockInstanceId != transformer.BlockInstanceId // 上記が一番早い起点となるジェネレーターでない場合はロックをする
                   )
                    return true;
                
                // 計算済みとして登録
                var gearRotationInfo = new GearRotationInfo(rpm, isClockwise, transformer);
                _checkedGearComponents.Add(transformer.BlockInstanceId, gearRotationInfo);
                
                // この歯車が接続している歯車を再帰的に計算する
                foreach (var connect in transformer.GetGearConnects())
                {
                    var isRocked = CalcGearInfo(connect, gearRotationInfo);
                    //ロックを検知したので処理を終了
                    if (isRocked) return true;
                }
                
                return false;
            }
            
            bool IsReverseRotation(GearConnect connect)
            {
                return connect.Self.IsReverse && connect.Target.IsReverse;
            }
            
            void SetRocked()
            {
                foreach (var transformer in GearTransformers) transformer.Rocked();
                foreach (var generator in GearGenerators) generator.Rocked();
            }
            
            void DistributeGearPower()
            {
                var totalGenerateTorque = new Torque(0);
                foreach (var gearGenerator in GearGenerators) totalGenerateTorque += gearGenerator.GenerateTorque;
                
                // 起点となるジェネレーターのRPM
                var originRpm = fastestOriginGenerator.GenerateRpm;
                //すべてのコンシューマーの必要トルクを取得し、生成トルクから割って分配率を計算する
                var totalRequiredTorquePerOriginRpm = 0f;
                
                foreach (var gearConsumer in GearTransformers)
                {
                    var info = _checkedGearComponents[gearConsumer.BlockInstanceId];
                    
                    var rpm = info.Rpm;
                    var isClockwise = info.IsClockwise;
                    
                    // このコンシューマーに供給できる最大のトルク
                    // このコンシューマーが要求するトルク
                    var requiredTorque = gearConsumer.GetRequiredTorque(rpm, isClockwise);
                    info.RequiredTorque = requiredTorque;
                    
                    // RPMの比によって供給するトルクを調整する
                    // RPMが倍であれば、その歯車に必要なトルクは倍になるし、RPMが半分であれば、その歯車に必要なトルクは半分になる
                    var distributeTorque = rpm.AsPrimitive() / originRpm.AsPrimitive() * requiredTorque.AsPrimitive();
                    totalRequiredTorquePerOriginRpm += distributeTorque;
                }
                
                // 分配率をもとに、供給するGPを算出し、RPMから供給トルクを計算する
                // 歯車とトルク両方に分配するため、平方根を取る
                var distributeGearPowerRate = Mathf.Sqrt(Mathf.Min(1, totalGenerateTorque.AsPrimitive() / totalRequiredTorquePerOriginRpm));
                
                foreach (var gearConsumer in GearTransformers)
                {
                    var info = _checkedGearComponents[gearConsumer.BlockInstanceId];
                    
                    var ratedDistributeTorque = new Torque(info.RequiredTorque.AsPrimitive() * distributeGearPowerRate * (originRpm / info.Rpm).AsPrimitive());
                    var ratedDistributionRpm = info.Rpm * distributeGearPowerRate;
                    
                    gearConsumer.SupplyPower(ratedDistributionRpm, ratedDistributeTorque, info.IsClockwise);
                }
                
                foreach (var generator in _gearGenerators)
                {
                    var info = _checkedGearComponents[generator.BlockInstanceId];
                    
                    var ratedDistributeTorque = new Torque(info.RequiredTorque.AsPrimitive() * distributeGearPowerRate);
                    var ratedDistributionRpm = info.Rpm * distributeGearPowerRate;
                    
                    generator.SupplyPower(ratedDistributionRpm, ratedDistributeTorque, info.IsClockwise);
                }
            }
            
            #endregion
        }
    }
    
    public class GearRotationInfo
    {
        public readonly IGearEnergyTransformer EnergyTransformer;
        public readonly bool IsClockwise;
        public readonly RPM Rpm;
        
        public GearRotationInfo(RPM rpm, bool isClockwise, IGearEnergyTransformer energyTransformer)
        {
            Rpm = rpm;
            IsClockwise = isClockwise;
            EnergyTransformer = energyTransformer;
        }
        
        public Torque RequiredTorque { get; set; }
    }
}