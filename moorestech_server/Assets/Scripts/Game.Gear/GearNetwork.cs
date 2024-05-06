using System;
using System.Collections.Generic;

namespace Game.Gear
{
    public class GearNetwork
    {
        private readonly List<IGearConsumer> _gearConsumers = new();
        private readonly Dictionary<int, IGearGenerator> _gearGeneratorMap = new();

        private readonly Dictionary<int, GearRotationInfo> _checkedGearComponents = new();

        public void CalculateSupplyPower()
        {
            //もっとも早いジェネレーターを選定、RPMを取得
            IGearGenerator fastGenerator = null;
            foreach (var gearGenerator in _gearGeneratorMap.Values)
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
                foreach (var consumer in _gearConsumers)
                {
                    consumer.SupplyPower(0, 0, true);
                }
                return;
            }

            //そのジェネレータと接続している各歯車コンポーネントを深さ優先度探索でたどり、RPMと回転方向を計算していく
            _checkedGearComponents.Clear();
            var generatorGearRotationInfo = new GearRotationInfo(fastGenerator.GenerateRpm, fastGenerator.IsClockwise, fastGenerator);
            CalcGearInfo(fastGenerator, generatorGearRotationInfo);

            //すべてのジェネレーターから生成GPを取得し、合算する
            var totalGeneratePower = 0f;
            foreach (var gearGenerator in _gearGeneratorMap.Values)
            {
                totalGeneratePower += gearGenerator.GeneratePower;
            }

            //すべてのコンシューマーの必要GPを取得し、生成GPから割って分配率を計算する
            var totalRequiredPower = 0f;
            foreach (var gearConsumer in _gearConsumers)
            {
                totalRequiredPower += gearConsumer.RequiredPower;
            }

            // 分配率をもとに、各コンシューマーに供給するGPを算出し、RPMから供給トルクを計算する
            var distributeRate = totalRequiredPower / totalGeneratePower;
            foreach (var gearConsumer in _gearConsumers)
            {
                var info = _checkedGearComponents[gearConsumer.EntityId];
                var supplyPower = gearConsumer.RequiredPower * distributeRate;

                var distributeTorque = supplyPower / info.Rpm;

                gearConsumer.SupplyPower(info.Rpm, distributeTorque, info.IsClockwise);
            }

            #region Internal

            bool CalcGearInfo(IGearComponent gearComponent, GearRotationInfo connectGearRotationInfo)
            {
                //RPMと回転方向を計算する
                var isClockwise = !connectGearRotationInfo.IsClockwise;
                var gearRate = (float)connectGearRotationInfo.GearComponent.TeethCount / gearComponent.TeethCount;
                var rpm = connectGearRotationInfo.Rpm * gearRate;

                // もし既に計算済みの場合、新たな計算と一致するかを計算し、一致しない場合はロックフラグを立てる
                if (_checkedGearComponents.TryGetValue(gearComponent.EntityId, out var info) &&
                    (info.IsClockwise != isClockwise || Math.Abs(info.Rpm - rpm) > 0.1f)) //回転方向が合っているか、RPMが合っているか
                {
                    return true;
                }

                // もしこれがジェネレーターである場合、回転方向が合っているかを確認し、合っていない場合はロックフラグを立てる
                if (_gearGeneratorMap.TryGetValue(gearComponent.EntityId, out var generator) &&
                    generator.IsClockwise != isClockwise)
                {
                    return true;
                }

                // 計算済みとして登録
                var gearRotationInfo = new GearRotationInfo(rpm, isClockwise, gearComponent);
                _checkedGearComponents.Add(gearComponent.EntityId, gearRotationInfo);

                // この歯車が接続している歯車を再帰的に計算する
                foreach (var connectingGear in gearComponent.ConnectingGears)
                {
                    var isRocked = CalcGearInfo(connectingGear, gearRotationInfo);
                    //ロックを検知したので処理を終了
                    if (isRocked) return true;
                }

                return false;
            }

            #endregion
        }
    }

    public class GearRotationInfo
    {
        public float Rpm;
        public bool IsClockwise;
        public IGearComponent GearComponent;
        public GearRotationInfo(float rpm, bool isClockwise, IGearComponent gearComponent)
        {
            Rpm = rpm;
            IsClockwise = isClockwise;
            GearComponent = gearComponent;
        }
    }
}