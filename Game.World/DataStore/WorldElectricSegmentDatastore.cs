using System.Collections.Generic;
using Core.Electric;
using World.Event;

namespace World.DataStore
{
    public class WorldElectricSegmentDatastore
    {
        private readonly Dictionary<int,ElectricSegment> _segmentDictionary = new();

        public WorldElectricSegmentDatastore()
        {
            
        }
    }
}