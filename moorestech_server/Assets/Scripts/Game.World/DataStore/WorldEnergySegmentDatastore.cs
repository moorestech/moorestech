using System;
using System.Collections.Generic;
using Game.EnergySystem;
using Game.World.Interface.DataStore;

namespace Game.World.DataStore
{
    public class WorldEnergySegmentDatastore<TSegment> : IWorldEnergySegmentDatastore<TSegment>
        where TSegment : EnergySegment, new()
    {
        private readonly List<TSegment> _segmentDictionary = new();
        
        //電柱オブジェクトから所属している電力セグメントを取得する
        public TSegment GetEnergySegment(IElectricTransformer transformer)
        {
            foreach (var segment in _segmentDictionary)
            {
                if (!segment.EnergyTransformers.ContainsKey(transformer.BlockInstanceId)) continue;
                return segment;
            }
            
            throw new Exception("電力セグメントが見つかりませんでした");
        }
        public TSegment GetEnergySegment(IElectricConsumer consumer)
        {
            foreach (var segment in _segmentDictionary)
            {
                if (!segment.Consumers.ContainsKey(consumer.BlockInstanceId)) continue;
                return segment;
            }
            
            throw new Exception("電力セグメントが見つかりませんでした");
        }
        public TSegment GetEnergySegment(IElectricGenerator generator)
        {
            foreach (var segment in _segmentDictionary)
            {
                if (!segment.Generators.ContainsKey(generator.BlockInstanceId)) continue;
                return segment;
            }
            
            throw new Exception("電力セグメントが見つかりませんでした");
        }
        
        public TSegment GetEnergySegment(int index)
        {
            return _segmentDictionary[index];
        }
        
        public TSegment CreateEnergySegment()
        {
            var electricSegment = new TSegment();
            _segmentDictionary.Add(electricSegment);
            return electricSegment;
        }
        
        public void SetEnergySegment(TSegment energySegment)
        {
            _segmentDictionary.Add(energySegment);
        }
        
        public void RemoveEnergySegment(TSegment energySegment)
        {
            _segmentDictionary.Remove(energySegment);
        }
        
        public int GetEnergySegmentListCount()
        {
            return _segmentDictionary.Count;
        }
    }
}