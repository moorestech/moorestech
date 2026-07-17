using System;
using System.Collections.Generic;
using Game.Block.Interface;

namespace Game.EnergySystem
{
    /// <summary>
    ///     電線で接続された一つの連結成分＝一つの電力セグメント。
    ///     tick処理は行わず、ElectricTickUpdaterから毎tick需給を再集計されて統計を確定する受動データ構造。
    ///     One connected component of the wire graph = one electric segment.
    ///     It performs no tick processing itself; ElectricTickUpdater re-aggregates supply and demand every tick and settles the statistics.
    /// </summary>
    public class EnergySegment
    {
        private readonly Dictionary<BlockInstanceId, IElectricConsumer> _consumers = new();
        private readonly Dictionary<BlockInstanceId, IElectricGenerator> _generators = new();

        private bool _isDestroyed;

        // このtickで確定した電力統計。需要0は供給率1として扱う
        // Statistics settled for this tick; zero demand is treated as supply rate 1
        public ElectricNetworkStatistics Statistics { get; private set; } = new(0f, 0f, 1f, 0);

        internal EnergySegment()
        {
        }

        // 毎tickの需給再集計と供給率確定。ElectricTickUpdaterからのみ呼ばれる
        // Re-aggregate supply and demand and settle the supply rate every tick; called only by ElectricTickUpdater
        internal ElectricNetworkStatistics SettleTick()
        {
            CheckDestroy();

            //供給されてる合計エネルギー量の算出
            var totalGenerate = new ElectricPower(0);
            foreach (var generator in _generators.Values) totalGenerate += generator.OutputEnergy();

            //エネルギーの需要量の算出
            var totalRequired = new ElectricPower(0);
            foreach (var consumer in _consumers.Values) totalRequired += consumer.RequestEnergy;

            // 供給率を算出。要求が0なら除算(NaN/Infinity)を避け、需要なしとして供給率1.0扱い
            // Compute supply rate; when demand is 0, avoid division (NaN/Infinity) and treat as no demand with rate 1.0
            var requiredPrimitive = totalRequired.AsPrimitive();
            var powerRate = requiredPrimitive <= 0f ? 1f : totalGenerate.AsPrimitive() / requiredPrimitive;
            if (1f < powerRate) powerRate = 1f;

            Statistics = new ElectricNetworkStatistics(totalGenerate.AsPrimitive(), requiredPrimitive, powerRate, _consumers.Count);
            return Statistics;
        }

        // 統計確定後の変換機等の電力tick後処理を実行する
        // Run the post-electric-tick processing (converters etc.) after the statistics are settled
        internal void RunPostTickProcess()
        {
            CheckDestroy();

            foreach (var generator in _generators.Values)
                if (generator is IElectricTickPostHandler handler)
                    handler.OnElectricTickPostProcess(Statistics);
            foreach (var consumer in _consumers.Values)
                if (consumer is IElectricTickPostHandler handler)
                    handler.OnElectricTickPostProcess(Statistics);
        }

        internal void AddEnergyConsumer(IElectricConsumer electricConsumer)
        {
            CheckDestroy();
            if (_consumers.ContainsKey(electricConsumer.BlockInstanceId)) return;
            _consumers.Add(electricConsumer.BlockInstanceId, electricConsumer);
        }

        internal void AddGenerator(IElectricGenerator generator)
        {
            CheckDestroy();
            if (_generators.ContainsKey(generator.BlockInstanceId)) return;
            _generators.Add(generator.BlockInstanceId, generator);
        }

        internal void Destroy()
        {
            if (_isDestroyed) return;

            _isDestroyed = true;

            // 各種Dictionaryをクリア
            _consumers.Clear();
            _generators.Clear();
        }

        private void CheckDestroy()
        {
            if (_isDestroyed)
            {
                throw new InvalidOperationException("This EnergySegment is already destroyed");
            }
        }
    }
}
