using System;
using Core.BeltTransport;
using static Tests.UnitTest.Core.BeltTransport.BeltConveyorSegmentTestUtil;

namespace Tests.UnitTest.Core.BeltTransport.Simulation.RandomNetwork
{
    // tick境界で公開TryReceiveから通常segmentへ供給する機械。合流の搬入元にはならない
    // A machine that feeds a normal segment through public TryReceive at tick boundaries. Never a merge input
    public sealed class RandomBeltMachineSource : IBeltSource
    {
        private readonly Random _random;
        private readonly BeltConveyorSegment _target;

        public RandomBeltMachineSource(int seed, BeltConveyorSegment target)
        {
            _random = new Random(seed);
            _target = target;
            target.AttachInput(this, BeltDirection.Back);
        }

        // 半分の確率で進入距離1～Wの搬入を試す。空きを超える進入距離は拒否される
        // Try an insert with entry length 1..W half of the time. Entry lengths above the offer are refused
        public bool TryInsert(long serial)
        {
            if (_random.Next(2) == 0) return false;
            var length = _random.Next(1, W + 1);
            return _target.TryReceive(BeltDirection.Back, length, MakeItem(serial));
        }

        public bool TryGetOutput(BeltDirection inputDirection)
        {
            return false;
        }
    }
}
