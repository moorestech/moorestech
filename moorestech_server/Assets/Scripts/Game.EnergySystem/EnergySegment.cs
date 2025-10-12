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
        
        
        private void Update()
        {
            CheckDestroy();

            //供給されてる合計エネルギー量の算出
            var powers = new ElectricPower(0);
            foreach (var key in _generators.Keys) powers += _generators[key].OutputEnergy();

            //エネルギーの需要量の算出
            var requester = new ElectricPower(0);
            foreach (var key in _consumers.Keys) requester += _consumers[key].RequestEnergy;

            //エネルギー供給の割合の算出
            var powerRate = powers / requester;
            if (1 < powerRate.AsPrimitive()) powerRate = new ElectricPower(1);

            //エネルギーを供給
            foreach (var key in _consumers.Keys)
                _consumers[key].SupplyEnergy(_consumers[key].RequestEnergy * powerRate);
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