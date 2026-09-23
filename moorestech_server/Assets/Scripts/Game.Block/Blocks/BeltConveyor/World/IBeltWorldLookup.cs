using System;
using Game.BeltSegment;
namespace Game.Block.Blocks.BeltConveyor
{
    public interface IBeltWorldLookup
    {
        BeltWorldSnapshot CaptureSnapshot();
        IObservable<BeltWorldSnapshot> OnBeltWorldRebuilt { get; }
        IObservable<BeltWorldFrame> OnFrame { get; }
    }
}
