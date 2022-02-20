using System;
using System.Collections.Generic;
using Core.Electric;
using Game.World.Interface.DataStore;
using World.Event;

namespace World.DataStore
{
    public class WorldElectricSegmentDatastore : IWorldElectricSegmentDatastore
    {
        private readonly List<ElectricSegment> _segmentDictionary = new();

        //電柱オブジェクトから所属している電力セグメントを取得する
        public ElectricSegment GetElectricSegment(IElectricPole pole)
        {
            foreach (var segment in _segmentDictionary)
            {
                if (!segment.ExistElectricPole(pole.GetEntityId())) continue;
                return segment;
            }

            throw new Exception("電力セグメントが見つかりませんでした");
        }

        public ElectricSegment GetElectricSegment(int index) { return _segmentDictionary[index]; }

        public ElectricSegment CreateElectricSegment()
        {
            var electricSegment = new ElectricSegment();
            _segmentDictionary.Add(electricSegment);
            return electricSegment;
        }

        public void SetElectricSegment(ElectricSegment electricSegment)
        {
            _segmentDictionary.Add(electricSegment);
        }

        public void RemoveElectricSegment(ElectricSegment electricSegment)
        {
            _segmentDictionary.Remove(electricSegment);
            electricSegment = null;
        }

        public int GetElectricSegmentListCount()
        {
            return _segmentDictionary.Count;
        }
    }
}