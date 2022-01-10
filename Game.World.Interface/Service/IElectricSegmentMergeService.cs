using System.Collections.Generic;
using Core.Electric;

namespace Game.World.Interface.Service
{
    public interface IElectricSegmentMergeService
    {
        public ElectricSegment MergeAndSetDatastoreElectricSegments(List<IElectricPole> poles);
    }
}