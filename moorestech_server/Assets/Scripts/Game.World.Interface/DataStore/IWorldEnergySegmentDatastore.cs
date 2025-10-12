using Game.EnergySystem;

namespace Game.World.Interface.DataStore
{
    public interface IWorldEnergySegmentDatastore<TSegment> where TSegment : EnergySegment, new()
    {
        public TSegment GetEnergySegment(IElectricTransformer transformer);
        public bool TryGetEnergySegment(IElectricConsumer consumer, out TSegment segment);
        public bool TryGetEnergySegment(IElectricGenerator generator, out TSegment segment);
        
        public TSegment GetEnergySegment(int index);
        public TSegment CreateEnergySegment();
        public void SetEnergySegment(TSegment energySegment);
        public void RemoveEnergySegment(TSegment energySegment);
        public int GetEnergySegmentListCount();
    }
}