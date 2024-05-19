using System;
using System.Collections.Generic;
using Game.World.Interface.DataStore;
using UnityEngine;

namespace Game.Gear.Common
{
    public class GearNetwork
    {
        public readonly int NetworkId;

        public IReadOnlyList<IGearEnergyTransformer> GearTransformers => _gearTransformers;
        private readonly List<IGearEnergyTransformer> _gearTransformers = new();

        public IReadOnlyList<IGearGenerator> GearGenerators => _gearGenerators;
        private readonly List<IGearGenerator> _gearGenerators = new();

        private Dictionary<int, GearRotationInfo> _checkedGearComponents = new();

        public GearNetwork(int networkId)
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

        public static IWorldBlockDatastore WorldBlockDatastore; // デバッグ用 後で消す

        public void ManualUpdate()
        {
            //もっとも早いジェネレーターを選定、RPMを取得
            IGearGenerator fastGenerator = null;
            foreach (var gearGenerator in GearGenerators)
            {
                if (fastGenerator == null)
                {
                    fastGenerator = gearGenerator;
                    continue;
                }
                if (gearGenerator.GenerateRpm > fastGenerator.GenerateRpm)
                {
                    fastGenerator = gearGenerator;
                }
            }

            if (fastGenerator == null)
            {
                //ジェネレーターがない場合はすべてにゼロを供給して終了
                foreach (var transformer in GearTransformers)
                {
                    transformer.SupplyPower(0, 0, true);
                }
                return;
            }

            //そのジェネレータと接続している各歯車コンポーネントを深さ優先度探索でたどり、RPMと回転方向を計算していく
            _checkedGearComponents.Clear();
            _checkedGearComponents = new();
            var generatorGearRotationInfo = new GearRotationInfo(fastGenerator.GenerateRpm, fastGenerator.GenerateIsClockwise, fastGenerator);
            var rocked = CalcGearInfo(fastGenerator, generatorGearRotationInfo);
            if (rocked)
            {
                foreach (var transformer in GearTransformers)
                {
                    transformer.Rocked();
                }
                foreach (var generator in GearGenerators)
                {
                    generator.Rocked();
                }
                return;
            }

            //すべてのジェネレーターから生成GPを取得し、合算する
            var totalGeneratePower = 0f;
            foreach (var gearGenerator in GearGenerators)
            {
                totalGeneratePower += gearGenerator.GeneratePower;
            }

            //すべてのコンシューマーの必要GPを取得し、生成GPから割って分配率を計算する
            var totalRequiredPower = 0f;
            foreach (var gearConsumer in GearTransformers)
            {
                totalRequiredPower += gearConsumer.RequiredPower;
            }

            // 分配率をもとに、各コンシューマーに供給するGPを算出し、RPMから供給トルクを計算する
            var distributeRate = totalRequiredPower / totalGeneratePower;
            foreach (var gearConsumer in GearTransformers)
            {
                var info = _checkedGearComponents[gearConsumer.EntityId];
                var supplyPower = gearConsumer.RequiredPower * distributeRate;

                var distributeTorque = supplyPower / info.Rpm;

                gearConsumer.SupplyPower(info.Rpm, distributeTorque, info.IsClockwise);
            }

            #region Internal

            bool CalcGearInfo(IGearEnergyTransformer transformer, GearRotationInfo connectGearRotationInfo)
            {
                //デバッグ用 後で消す
                var name = WorldBlockDatastore.GetBlock(transformer.EntityId).BlockConfigData.Name;
                var connectName = WorldBlockDatastore.GetBlock(connectGearRotationInfo.EnergyTransformer.EntityId).BlockConfigData.Name;

                //RPMと回転方向を計算する
                var isReverseRotation = IsReverseRotation(transformer, connectGearRotationInfo);
                var isClockwise = isReverseRotation ? !connectGearRotationInfo.IsClockwise : connectGearRotationInfo.IsClockwise;
                var rpm = 0f;
                if (transformer is IGear gear &&
                    connectGearRotationInfo.EnergyTransformer is IGear connectGear)
                {
                    var gearRate = (float)connectGear.TeethCount / gear.TeethCount;
                    rpm = connectGearRotationInfo.Rpm * gearRate;
                }
                else
                {
                    rpm = connectGearRotationInfo.Rpm;
                }

                // もし既に計算済みの場合、新たな計算と一致するかを計算し、一致しない場合はロックフラグを立てる
                if (_checkedGearComponents.TryGetValue(transformer.EntityId, out var info))
                {
                    if (transformer.IsReverseRotation && info.IsClockwise == isClockwise || // 回転方向を逆転するのに逆転できてない場合
                        !transformer.IsReverseRotation && info.IsClockwise != isClockwise || // 回転方向を逆転しないのに逆転している場合
                        Math.Abs(info.Rpm - rpm) > 0.1f) // RPMが一致しない場合
                    {
                        return true;
                    }

                    // 深さ優先度探索でループになったのでこの探索は終了
                    return false;
                }

                if (transformer is IGearGenerator generator
                    && generator.GenerateIsClockwise != isClockwise // もしこれがジェネレーターである場合、回転方向が合っているかを確認
                    && fastGenerator.EntityId != transformer.EntityId // 上記が一番早い起点となるジェネレーターでない場合はロックをする
                   )
                {
                    return true;
                }

                // 計算済みとして登録
                var gearRotationInfo = new GearRotationInfo(rpm, isClockwise, transformer);
                _checkedGearComponents.Add(transformer.EntityId, gearRotationInfo);

                if (name == "SmallTestGear")
                {
                    Debug.Log(name);
                }

                // この歯車が接続している歯車を再帰的に計算する
                foreach (var connectingGear in transformer.ConnectingTransformers)
                {
                    var isRocked = CalcGearInfo(connectingGear, gearRotationInfo);
                    //ロックを検知したので処理を終了
                    if (isRocked) return true;
                }

                return false;
            }

            bool IsReverseRotation(IGearEnergyTransformer transformer, GearRotationInfo connectGearRotationInfo)
            {
                return transformer.IsReverseRotation && connectGearRotationInfo.EnergyTransformer.IsReverseRotation;
            }

            #endregion
        }
    }

    public class GearRotationInfo
    {
        public readonly float Rpm;
        public readonly bool IsClockwise;
        public readonly IGearEnergyTransformer EnergyTransformer;
        public GearRotationInfo(float rpm, bool isClockwise, IGearEnergyTransformer energyTransformer)
        {
            Rpm = rpm;
            IsClockwise = isClockwise;
            EnergyTransformer = energyTransformer;
        }
    }
}