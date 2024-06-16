﻿using System.Collections.Generic;
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
        
        public EnergySegment()
        {
            GameUpdater.UpdateObservable.Subscribe(_ => Update());
        }
        
        public IReadOnlyDictionary<BlockInstanceId, IElectricConsumer> Consumers => _consumers;
        
        public IReadOnlyDictionary<BlockInstanceId, IElectricGenerator> Generators => _generators;
        
        public IReadOnlyDictionary<BlockInstanceId, IElectricTransformer> EnergyTransformers => _energyTransformers;
        
        private void Update()
        {
            //供給されてる合計エネルギー量の算出
            ElectricPower powers = 0;
            foreach (var key in _generators.Keys) powers += _generators[key].OutputEnergy();
            
            //エネルギーの需要量の算出
            ElectricPower requester = 0;
            foreach (var key in _consumers.Keys) requester += _consumers[key].RequestEnergy;
            
            //エネルギー供給の割合の算出
            var powerRate = powers / (double)requester;
            if (1 < powerRate) powerRate = 1;
            
            //エネルギーを供給
            foreach (var key in _consumers.Keys)
                _consumers[key].SupplyEnergy(_consumers[key].RequestEnergy * (ElectricPower)powerRate);
        }
        
        public void AddEnergyConsumer(IElectricConsumer electricConsumer)
        {
            if (_consumers.ContainsKey(electricConsumer.BlockInstanceId)) return;
            _consumers.Add(electricConsumer.BlockInstanceId, electricConsumer);
        }
        
        public void RemoveEnergyConsumer(IElectricConsumer electricConsumer)
        {
            if (!_consumers.ContainsKey(electricConsumer.BlockInstanceId)) return;
            _consumers.Remove(electricConsumer.BlockInstanceId);
        }
        
        public void AddGenerator(IElectricGenerator generator)
        {
            if (_generators.ContainsKey(generator.BlockInstanceId)) return;
            _generators.Add(generator.BlockInstanceId, generator);
        }
        
        public void RemoveGenerator(IElectricGenerator generator)
        {
            if (!_generators.ContainsKey(generator.BlockInstanceId)) return;
            _generators.Remove(generator.BlockInstanceId);
        }
        
        public void AddEnergyTransformer(IElectricTransformer electricTransformer)
        {
            if (_energyTransformers.ContainsKey(electricTransformer.BlockInstanceId)) return;
            _energyTransformers.Add(electricTransformer.BlockInstanceId, electricTransformer);
        }
        
        public void RemoveEnergyTransformer(IElectricTransformer electricTransformer)
        {
            if (!_energyTransformers.ContainsKey(electricTransformer.BlockInstanceId)) return;
            _energyTransformers.Remove(electricTransformer.BlockInstanceId);
        }
    }
}