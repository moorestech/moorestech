using System;
using System.Collections.Generic;
using Core.BeltTransport;
using static Tests.UnitTest.Core.BeltTransport.BeltConveyorSegmentTestUtil;

namespace Tests.UnitTest.Core.BeltTransport.Simulation.RandomNetwork
{
    // tick境界で開閉が決まり、開いていても確率で拒否する機械。受け取ったアイテムを記録する
    // A machine opened or closed at each tick boundary that may still refuse randomly. Records received items
    public sealed class RandomBeltMachineSink : IBeltReceiver
    {
        public readonly List<BeltItem> Delivered = new();
        private readonly Random _random;
        private int _offer;

        public RandomBeltMachineSink(int seed)
        {
            _random = new Random(seed);
        }

        // 7割で開き、空きは1～W。閉じている間は空き0
        // Open 70% of the time with an offer of 1..W; offer 0 while closed
        public void RerollAtTickBoundary()
        {
            _offer = _random.Next(10) < 7 ? _random.Next(1, W + 1) : 0;
        }

        public void AttachInput(IBeltSource source, BeltDirection inputDirection)
        {
        }

        public int GetOffer(BeltDirection inputDirection)
        {
            return _offer;
        }

        public bool TryReceive(BeltDirection inputDirection, int length, in BeltItem item)
        {
            if (_offer <= 0 || _random.Next(5) == 0) return false;
            Delivered.Add(item);
            return true;
        }
    }
}
