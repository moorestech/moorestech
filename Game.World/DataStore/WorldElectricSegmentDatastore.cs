using System.Collections.Generic;
using Core.Electric;
using Game.World.Interface.DataStore;
using World.Event;

namespace World.DataStore
{
    public class WorldElectricSegmentDatastore : IWorldElectricSegmentDatastore
    {
        private readonly List<ElectricSegment> _segmentDictionary = new();

        public WorldElectricSegmentDatastore()
        {
            
        }
        
        //TODO 電柱オブジェクトから所属している電力セグメントを取得する
        public ElectricSegment GetElectricSegment(IElectricPole pole)
        {
            return null;
        }

        public ElectricSegment GetElectricSegment(int index)
        {
            return _segmentDictionary[index];   
        }

        public ElectricSegment CreateElectricSegment()
        {
            var electricSegment = new ElectricSegment();
            _segmentDictionary.Add(electricSegment);
            return electricSegment;
        }
        public int GetListCount() { return _segmentDictionary.Count; }
    }
}