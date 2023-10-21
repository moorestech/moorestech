using System.Collections.Generic;
using Core.Update;

namespace Core.EnergySystem
{
    /// <summary>
    ///     
    /// </summary>
    public class EnergySegment : IUpdatable
    {
        private readonly Dictionary<int, IEnergyConsumer> _consumers = new();
        private readonly Dictionary<int, IEnergyTransformer> _energyTransformers = new();
        private readonly Dictionary<int, IEnergyGenerator> _generators = new();

        public EnergySegment()
        {
            GameUpdater.RegisterUpdater(this);
        }

        public IReadOnlyDictionary<int, IEnergyConsumer> Consumers => _consumers;

        public IReadOnlyDictionary<int, IEnergyGenerator> Generators => _generators;

        public IReadOnlyDictionary<int, IEnergyTransformer> EnergyTransformers => _energyTransformers;

        public void Update()
        {
            
            var powers = 0;
            foreach (var key in _generators.Keys) powers += _generators[key].OutputEnergy();

            
            var requester = 0;
            foreach (var key in _consumers.Keys) requester += _consumers[key].RequestEnergy;

            
            var powerRate = powers / (double)requester;
            if (1 < powerRate) powerRate = 1;

            
            foreach (var key in _consumers.Keys) _consumers[key].SupplyEnergy((int)(_consumers[key].RequestEnergy * powerRate));
        }

        public void AddEnergyConsumer(IEnergyConsumer energyConsumer)
        {
            if (_consumers.ContainsKey(energyConsumer.EntityId)) return;
            _consumers.Add(energyConsumer.EntityId, energyConsumer);
        }

        public void RemoveEnergyConsumer(IEnergyConsumer energyConsumer)
        {
            if (!_consumers.ContainsKey(energyConsumer.EntityId)) return;
            _consumers.Remove(energyConsumer.EntityId);
        }

        public void AddGenerator(IEnergyGenerator generator)
        {
            if (_generators.ContainsKey(generator.EntityId)) return;
            _generators.Add(generator.EntityId, generator);
        }

        public void RemoveGenerator(IEnergyGenerator generator)
        {
            if (!_generators.ContainsKey(generator.EntityId)) return;
            _generators.Remove(generator.EntityId);
        }

        public void AddEnergyTransformer(IEnergyTransformer energyTransformer)
        {
            if (_energyTransformers.ContainsKey(energyTransformer.EntityId)) return;
            _energyTransformers.Add(energyTransformer.EntityId, energyTransformer);
        }

        public void RemoveEnergyTransformer(IEnergyTransformer energyTransformer)
        {
            if (!_energyTransformers.ContainsKey(energyTransformer.EntityId)) return;
            _energyTransformers.Remove(energyTransformer.EntityId);
        }
    }
}