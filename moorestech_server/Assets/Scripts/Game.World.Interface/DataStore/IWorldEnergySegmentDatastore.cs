using Game.EnergySystem;

namespace Game.World.Interface.DataStore
{
    public interface IWorldEnergySegmentDatastore<TSegment> where TSegment : EnergySegment, new()
    {
        public TSegment GetEnergySegment(IElectricTransformer transformer);
        public TSegment GetEnergySegment(IElectricConsumer consumer);
        public TSegment GetEnergySegment(IElectricGenerator generator);
        
        public TSegment GetEnergySegment(int index);
        public TSegment CreateEnergySegment();
        public void SetEnergySegment(TSegment energySegment);
        public void RemoveEnergySegment(TSegment energySegment);
        public int GetEnergySegmentListCount();
    }
}