using System;
using Game.BeltSegment;
namespace Game.Block.Blocks.BeltConveyor
{
    public interface IBeltWorldLookup
    {
        BeltWorldSnapshot CaptureSnapshot();
        IObservable<BeltWorldSnapshot> OnRebuilt { get; }
        IObservable<BeltWorldFrame> OnFrame { get; }
    }
}
