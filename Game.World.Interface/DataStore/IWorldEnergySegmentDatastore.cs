using Core.EnergySystem;

namespace Game.World.Interface.DataStore
{
    public interface IWorldEnergySegmentDatastore<TSegment> where TSegment : EnergySegment, new()
    {
        public TSegment GetEnergySegment(IEnergyTransformer transformer);
        public TSegment GetEnergySegment(int index);
        public TSegment CreateEnergySegment();
        public void SetEnergySegment(TSegment energySegment);
        public void RemoveEnergySegment(TSegment energySegment);
        public int GetEnergySegmentListCount();
    }
}