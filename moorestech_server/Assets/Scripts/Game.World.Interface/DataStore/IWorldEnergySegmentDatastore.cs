using Game.Block.Interface;
using Game.EnergySystem;

namespace Game.World.Interface.DataStore
{
    public interface IWorldEnergySegmentDatastore<TSegment> where TSegment : EnergySegment, new()
    {
        public TSegment GetEnergySegment(IElectricTransformer transformer);
        public bool TryGetEnergySegment(IElectricConsumer consumer, out TSegment segment);
        public bool TryGetEnergySegment(IElectricGenerator generator, out TSegment segment);

        // 消費者・発電機・電柱いずれかのBlockInstanceIdから所属セグメントを引く
        // Resolve the owning segment from a BlockInstanceId of a consumer, generator, or transformer
        public bool TryGetEnergySegment(BlockInstanceId blockInstanceId, out TSegment segment);
        
        public TSegment GetEnergySegment(int index);
        public TSegment CreateEnergySegment();
        public void SetEnergySegment(TSegment energySegment);
        public void RemoveEnergySegment(TSegment energySegment);
        public int GetEnergySegmentListCount();
    }
}