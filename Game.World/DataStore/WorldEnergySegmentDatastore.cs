using System;
using System.Collections.Generic;
using Core.Electric;
using Core.EnergySystem;
using Game.World.Interface.DataStore;
using World.Event;

namespace World.DataStore
{
    public class WorldEnergySegmentDatastore<TSegment> : IWorldEnergySegmentDatastore<TSegment>  where TSegment : EnergySegment,new ()
    {
        private readonly List<TSegment> _segmentDictionary = new();
        
        //電柱オブジェクトから所属している電力セグメントを取得する
        public TSegment GetEnergySegment(IEnergyTransformer transformer)
        {
            foreach (var segment in _segmentDictionary)
            {
                if (!segment.EnergyTransformers.ContainsKey(transformer.EntityId)) continue;
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
            energySegment = null;
        }

        public int GetEnergySegmentListCount()
        {
            return _segmentDictionary.Count;
        }
    }
}