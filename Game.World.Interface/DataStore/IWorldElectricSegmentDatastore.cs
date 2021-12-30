using Core.Electric;

namespace Game.World.Interface.DataStore
{
    public interface IWorldElectricSegmentDatastore
    {
        public ElectricSegment GetElectricSegment(IElectricPole pole);
        public ElectricSegment CreateElectricSegment(int id);
    }
}