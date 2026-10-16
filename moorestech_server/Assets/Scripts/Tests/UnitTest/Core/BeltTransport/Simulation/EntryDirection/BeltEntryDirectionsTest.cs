using Core.BeltTransport;
using NUnit.Framework;

namespace Tests.UnitTest.Core.BeltTransport.Simulation.EntryDirection
{
    public class BeltEntryDirectionsTest
    {
        [TestCase(BeltDirection.Front, BeltEntryDirection.FromFront, BeltEntryDirection.FromFrontAbove, BeltEntryDirection.FromFrontBelow)]
        [TestCase(BeltDirection.Back, BeltEntryDirection.FromBack, BeltEntryDirection.FromBackAbove, BeltEntryDirection.FromBackBelow)]
        [TestCase(BeltDirection.Left, BeltEntryDirection.FromLeft, BeltEntryDirection.FromLeftAbove, BeltEntryDirection.FromLeftBelow)]
        [TestCase(BeltDirection.Right, BeltEntryDirection.FromRight, BeltEntryDirection.FromRightAbove, BeltEntryDirection.FromRightBelow)]
        public void 搬入元の水平方向と高さから12通りの進入方向を作り水平方向へ戻せる(
            BeltDirection inputDirection, BeltEntryDirection level, BeltEntryDirection above, BeltEntryDirection below)
        {
            Assert.AreEqual(level, BeltEntryDirections.Level(inputDirection));
            Assert.AreEqual(above, BeltEntryDirections.FromAbove(inputDirection));
            Assert.AreEqual(below, BeltEntryDirections.FromBelow(inputDirection));
            Assert.AreEqual(inputDirection, BeltEntryDirections.Horizontal(level));
            Assert.AreEqual(inputDirection, BeltEntryDirections.Horizontal(above));
            Assert.AreEqual(inputDirection, BeltEntryDirections.Horizontal(below));
        }
    }
}
