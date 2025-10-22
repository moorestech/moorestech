using System;
using System.Collections.Generic;
using Game.Block.Interface;
using UnityEngine;

namespace Game.Gear.Common
{
    public class GearNetwork
    {
        public IReadOnlyList<IGearEnergyTransformer> GearTransformers => _gearTransformers;
        public IReadOnlyList<IGearGenerator> GearGenerators => _gearGenerators;
        
        public GearNetworkInfo CurrentGearNetworkInfo { get; private set; }
        
        private readonly Dictionary<BlockInstanceId, GearRotationInfo> _checkedGearComponents = new();
        private readonly List<IGearGenerator> _gearGenerators = new();
        private readonly List<IGearEnergyTransformer> _gearTransformers = new();
        public readonly GearNetworkId NetworkId;
        
        public GearNetwork(GearNetworkId networkId)
        {
            NetworkId = networkId;
        }
        
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
                CurrentGearNetworkInfo = GearNetworkInfo.CreateEmpty();
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
                CurrentGearNetworkInfo = new GearNetworkInfo(0, 0, 0, GearNetworkStopReason.Rocked);
                SetNetworkStop();
                return;
            }

            // エネルギー収支を計算し、不足している場合はネットワークを停止
            var (totalRequiredGearPower, totalGeneratePower) = CalculateEnergyBalance();
            if (totalRequiredGearPower > totalGeneratePower)
            {
                CurrentGearNetworkInfo = new GearNetworkInfo(totalRequiredGearPower, totalGeneratePower, 0f, GearNetworkStopReason.OverRequirePower);
                SetNetworkStop();
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
            
            void SetNetworkStop()
            {
                foreach (var transformer in GearTransformers) transformer.StopNetwork();
                foreach (var generator in GearGenerators) generator.StopNetwork();
            }

            (float totalRequiredGearPower, float totalGeneratePower) CalculateEnergyBalance()
            {
                // 要求されているギアパワーを算出
                var totalRequired = 0f;
                foreach (var transformer in GearTransformers)
                {
                    var info = _checkedGearComponents[transformer.BlockInstanceId];
                    totalRequired += info.RequiredTorque.AsPrimitive() * info.Rpm.AsPrimitive();
                }

                // 生成されるギアパワーを算出
                var totalGenerate = 0f;
                foreach (var generator in GearGenerators)
                {
                    totalGenerate += generator.GenerateTorque.AsPrimitive() * generator.GenerateRpm.AsPrimitive();
                }

                return (totalRequired, totalGenerate);
            }
            
            // ここのロジックはドキュメント参照
            // https://splashy-relative-f65.notion.site/923bdb103c434e629e7a22b4e1618fdf?pvs=4
            void DistributeGearPower()
            {
                // 要求されているギアパワーを算出
                var totalRequiredGearPower = 0f;
                foreach (var transformer in GearTransformers)
                {
                    var info = _checkedGearComponents[transformer.BlockInstanceId];
                    
                    totalRequiredGearPower += info.RequiredTorque.AsPrimitive() * info.Rpm.AsPrimitive();
                }
                
                // 生成されるギアパワーを算出
                var totalGeneratePower = 0f;
                foreach (var generator in GearGenerators)
                {
                    totalGeneratePower += generator.GenerateTorque.AsPrimitive() * generator.GenerateRpm.AsPrimitive();
                }
                
                // 要求されたトルクの量が供給量を上回ってるとき、その量に応じてRPMを減速させる
                var rpmRate = totalRequiredGearPower == 0 ? 1 : Mathf.Min(1, totalGeneratePower / totalRequiredGearPower);
                
                CurrentGearNetworkInfo = new GearNetworkInfo(totalRequiredGearPower, totalGeneratePower, rpmRate, GearNetworkStopReason.None);
                
                // 生成されるギアパワーを各歯車コンポーネントに供給する
                foreach (var transformer in GearTransformers)
                {
                    var info = _checkedGearComponents[transformer.BlockInstanceId];
                    
                    var supplyTorque = info.RequiredTorque / totalRequiredGearPower * totalGeneratePower;
                    if (float.IsNaN(supplyTorque.AsPrimitive()))
                    {
                        supplyTorque = new Torque(0);
                    }
                    // 要求トルク以上のトルクが供給されないようにする
                    supplyTorque = new Torque(Mathf.Min(supplyTorque.AsPrimitive(), info.RequiredTorque.AsPrimitive()));
                    
                    var supplyRpm = info.Rpm * rpmRate;
                    
                    transformer.SupplyPower(supplyRpm, supplyTorque, info.IsClockwise);
                }
                
                foreach (var generator in GearGenerators)
                {
                    var info = _checkedGearComponents[generator.BlockInstanceId];
                    
                    var supplyRpm = info.Rpm * rpmRate;
                    
                    generator.SupplyPower(supplyRpm, generator.GenerateTorque, info.IsClockwise);
                }
            }
            
            #endregion
        }
    }
    
    public class GearRotationInfo
    {
        public readonly RPM Rpm;
        public readonly bool IsClockwise;
        
        public readonly Torque RequiredTorque;
        public readonly IGearEnergyTransformer EnergyTransformer;
        
        public GearRotationInfo(RPM rpm, bool isClockwise, IGearEnergyTransformer energyTransformer)
        {
            Rpm = rpm;
            IsClockwise = isClockwise;
            EnergyTransformer = energyTransformer;
            RequiredTorque = energyTransformer.GetRequiredTorque(rpm, isClockwise);
        }
    }
}