using System.Collections.Generic;
using Core.Electric;

namespace Game.World.Interface.DataStore
{
    public interface IWorldElectricSegmentDatastore
    {
        public ElectricSegment GetElectricSegment(IElectricPole pole);
        public ElectricSegment GetElectricSegment(int index);
        public ElectricSegment CreateElectricSegment();
        public void SetElectricSegment(ElectricSegment electricSegment);
        public void RemoveElectricSegment(ElectricSegment electricSegment);
        public int GetElectricSegmentListCount();
    }
}