using Game.Train.RailGraph;
using NUnit.Framework;

namespace Tests.UnitTest.Game
{
    public class TrainRailNodeIdAllocatorTest
    {
        [Test]
        public void RentReusesReleasedPairsExactlyAsDocumented()
        {
            // 連続貸出と解放でコメント例の挙動を確認する
            // Validate allocator behavior described in the class comment
            var allocator = new RailNodeIdAllocator(_ => { });
            _ = allocator.Rent2(); // 0,1
            _ = allocator.Rent2(); // 2,3
            _ = allocator.Rent2(); // 4,5

            allocator.Return(2);
            allocator.Return(3);

            var reusedSingle = allocator.Rent();
            Assert.AreEqual(2, reusedSingle);

            allocator.Return(reusedSingle);

            var reusedPair = allocator.Rent2();
            Assert.AreEqual((2, 3), reusedPair);
        }

        [Test]
        public void RentSkipsHalfReleasedPairsUntilBothSidesReturn()
        {
            // 片側だけ解放した場合のシーケンスを検証する
            // Ensure allocator skips pairs when only one side is released
            var allocator = new RailNodeIdAllocator(_ => { });
            _ = allocator.Rent2(); // 0,1
            _ = allocator.Rent2(); // 2,3
            _ = allocator.Rent2(); // 4,5

            allocator.Return(3);

            var rentAfterSingleRelease = allocator.Rent();
            Assert.AreEqual(6, rentAfterSingleRelease);

            allocator.Return(2);

            var reuseAfterFullRelease = allocator.Rent();
            Assert.AreEqual(2, reuseAfterFullRelease);

            var nextSequential = allocator.Rent();
            Assert.AreEqual(8, nextSequential);
        }
    }
}
