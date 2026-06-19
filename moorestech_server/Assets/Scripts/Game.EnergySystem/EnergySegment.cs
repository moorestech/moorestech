using System;
using System.Collections.Generic;
using Core.Update;
using Game.Block.Interface;
using UniRx;

namespace Game.EnergySystem
{
    /// <summary>
    ///     そのエネルギーの供給、配分を行うシステム
    /// </summary>
    public class EnergySegment
    {
        private readonly Dictionary<BlockInstanceId, IElectricConsumer> _consumers = new();
        private readonly Dictionary<BlockInstanceId, IElectricTransformer> _energyTransformers = new();
        private readonly Dictionary<BlockInstanceId, IElectricGenerator> _generators = new();

        private readonly IDisposable _updateSubscription;

        public bool IsDestroyed { get; private set; }

        public EnergySegment()
        {
            _updateSubscription = GameUpdater.UpdateObservable.Subscribe(_ => Update());
        }

        public IReadOnlyDictionary<BlockInstanceId, IElectricConsumer> Consumers => _consumers;

        public IReadOnlyDictionary<BlockInstanceId, IElectricGenerator> Generators => _generators;

        public IReadOnlyDictionary<BlockInstanceId, IElectricTransformer> EnergyTransformers => _energyTransformers;
        
        
        // 現時点のネットワーク集約統計を返す。SupplyEnergyを呼ばないので副作用はない
        // Return the current aggregated network statistics; this never calls SupplyEnergy so it has no side effects
        public ElectricNetworkStatistics GetCurrentStatistics()
        {
            CheckDestroy();
            return CalculateStatistics();
        }

        private void Update()
        {
            CheckDestroy();

            // 集約統計を算出し、供給率に応じて各消費者へエネルギーを配分
            // Calculate aggregated statistics and distribute energy to each consumer by the supply rate
            var statistics = CalculateStatistics();
            var powerRate = new ElectricPower(statistics.PowerRate);
            foreach (var consumer in _consumers.Values)
                consumer.SupplyEnergy(consumer.RequestEnergy * powerRate);
        }

        // 発電合計・要求合計・供給率を算出する純粋な計算。UpdateとGetCurrentStatisticsで共有
        // Pure computation of total generation, total demand, and supply rate; shared by Update and GetCurrentStatistics
        private ElectricNetworkStatistics CalculateStatistics()
        {
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
            if (powerRate > 1f) powerRate = 1f;

            return new ElectricNetworkStatistics(totalGenerate.AsPrimitive(), requiredPrimitive, powerRate, _generators.Count, _consumers.Count);
        }
        
        public void AddEnergyConsumer(IElectricConsumer electricConsumer)
        {
            CheckDestroy();
            if (_consumers.ContainsKey(electricConsumer.BlockInstanceId)) return;
            _consumers.Add(electricConsumer.BlockInstanceId, electricConsumer);
        }

        public void RemoveEnergyConsumer(IElectricConsumer electricConsumer)
        {
            CheckDestroy();
            if (!_consumers.ContainsKey(electricConsumer.BlockInstanceId)) return;
            _consumers.Remove(electricConsumer.BlockInstanceId);
        }

        public void AddGenerator(IElectricGenerator generator)
        {
            CheckDestroy();
            if (_generators.ContainsKey(generator.BlockInstanceId)) return;
            _generators.Add(generator.BlockInstanceId, generator);
        }

        public void RemoveGenerator(IElectricGenerator generator)
        {
            CheckDestroy();
            if (!_generators.ContainsKey(generator.BlockInstanceId)) return;
            _generators.Remove(generator.BlockInstanceId);
        }

        public void AddEnergyTransformer(IElectricTransformer electricTransformer)
        {
            CheckDestroy();
            if (_energyTransformers.ContainsKey(electricTransformer.BlockInstanceId)) return;
            _energyTransformers.Add(electricTransformer.BlockInstanceId, electricTransformer);
        }

        public void RemoveEnergyTransformer(IElectricTransformer electricTransformer)
        {
            CheckDestroy();
            if (!_energyTransformers.ContainsKey(electricTransformer.BlockInstanceId)) return;
            _energyTransformers.Remove(electricTransformer.BlockInstanceId);
        }

        public void Destroy()
        {
            if (IsDestroyed) return;

            IsDestroyed = true;

            // Updateの購読を解除
            _updateSubscription.Dispose();

            // 各種Dictionaryをクリア
            _consumers.Clear();
            _generators.Clear();
            _energyTransformers.Clear();
        }
        
        private void CheckDestroy()
        {
            if (IsDestroyed)
            {
                throw new InvalidOperationException("This EnergySegment is already destroyed");
            }
        }
    }
}