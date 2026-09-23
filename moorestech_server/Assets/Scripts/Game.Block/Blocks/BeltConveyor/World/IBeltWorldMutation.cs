using Core.Item.Interface;
using Game.Block.Interface.Component;
namespace Game.Block.Blocks.BeltConveyor
{
    public interface IBeltWorldMutation
    {
        void Register(SegmentBeltComponent component);
        void Unregister(SegmentBeltComponent component);
        void BeginTick(ulong tick);
        void CompleteTick();
        IItemStack Insert(SegmentBeltComponent target, IItemStack stack, InsertItemContext context);
        IItemStack GetCellItem(SegmentBeltComponent target);
        void SetCellItem(SegmentBeltComponent target, IItemStack stack);
        BeltCellSaveState CaptureCell(SegmentBeltComponent target);
    }
}
