using System.Collections.Generic;
using Core.Electric;
using Game.World.Interface.DataStore;
using World.Event;

namespace World.DataStore
{
    public class WorldElectricSegmentDatastore : IWorldElectricSegmentDatastore
    {
        private readonly Dictionary<int,ElectricSegment> _segmentDictionary = new();

        public WorldElectricSegmentDatastore()
        {
            
        }
        
        //TODO 電柱オブジェクトから所属している電力セグメントを取得する
        public ElectricSegment GetElectricSegment(IElectricPole pole)
        {
            return null;
        }
        public ElectricSegment CreateElectricSegment(int id)
        {
            var electricSegment = new ElectricSegment();
            _segmentDictionary.Add(id,electricSegment);
            return electricSegment;
        }
    }
}