using System.Collections.Generic;
using Core.Electric;

namespace Game.World.Interface.DataStore
{
    public interface IWorldElectricSegmentDatastore
    {
        public ElectricSegment GetElectricSegment(IElectricPole pole);
        public ElectricSegment GetElectricSegment(int index);
        public ElectricSegment CreateElectricSegment();
        public ElectricSegment MergedElectricSegments(List<IElectricPole> poles);
        public int GetElectricSegmentListCount();
    }
}