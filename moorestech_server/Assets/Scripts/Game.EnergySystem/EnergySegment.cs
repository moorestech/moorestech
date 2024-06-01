using System.Collections.Generic;
using Core.Update;
using UniRx;

namespace Game.EnergySystem
{
    /// <summary>
    ///     そのエネルギーの供給、配分を行うシステム
    /// </summary>
    public class EnergySegment
    {
        private readonly Dictionary<int, IElectricConsumer> _consumers = new();
        private readonly Dictionary<int, IElectricTransformer> _energyTransformers = new();
        private readonly Dictionary<int, IElectricGenerator> _generators = new();

        public EnergySegment()
        {
            GameUpdater.UpdateObservable.Subscribe(_ => Update());
        }

        public IReadOnlyDictionary<int, IElectricConsumer> Consumers => _consumers;

        public IReadOnlyDictionary<int, IElectricGenerator> Generators => _generators;

        public IReadOnlyDictionary<int, IElectricTransformer> EnergyTransformers => _energyTransformers;

        private void Update()
        {
            //供給されてる合計エネルギー量の算出
            var powers = 0;
            foreach (var key in _generators.Keys) powers += _generators[key].OutputEnergy();

            //エネルギーの需要量の算出
            var requester = 0;
            foreach (var key in _consumers.Keys) requester += _consumers[key].RequestEnergy;

            //エネルギー供給の割合の算出
            var powerRate = powers / (double)requester;
            if (1 < powerRate) powerRate = 1;

            //エネルギーを供給
            foreach (var key in _consumers.Keys)
                _consumers[key].SupplyEnergy((int)(_consumers[key].RequestEnergy * powerRate));
        }

        public void AddEnergyConsumer(IElectricConsumer electricConsumer)
        {
            if (_consumers.ContainsKey(electricConsumer.EntityId)) return;
            _consumers.Add(electricConsumer.EntityId, electricConsumer);
        }

        public void RemoveEnergyConsumer(IElectricConsumer electricConsumer)
        {
            if (!_consumers.ContainsKey(electricConsumer.EntityId)) return;
            _consumers.Remove(electricConsumer.EntityId);
        }

        public void AddGenerator(IElectricGenerator generator)
        {
            if (_generators.ContainsKey(generator.EntityId)) return;
            _generators.Add(generator.EntityId, generator);
        }

        public void RemoveGenerator(IElectricGenerator generator)
        {
            if (!_generators.ContainsKey(generator.EntityId)) return;
            _generators.Remove(generator.EntityId);
        }

        public void AddEnergyTransformer(IElectricTransformer electricTransformer)
        {
            if (_energyTransformers.ContainsKey(electricTransformer.EntityId)) return;
            _energyTransformers.Add(electricTransformer.EntityId, electricTransformer);
        }

        public void RemoveEnergyTransformer(IElectricTransformer electricTransformer)
        {
            if (!_energyTransformers.ContainsKey(electricTransformer.EntityId)) return;
            _energyTransformers.Remove(electricTransformer.EntityId);
        }
    }
}