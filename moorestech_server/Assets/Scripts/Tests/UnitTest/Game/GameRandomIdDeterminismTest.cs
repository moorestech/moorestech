using Core.Update;
using Game.Block.Interface;
using Game.Train.Unit;
using NUnit.Framework;

namespace Tests.UnitTest.Game
{
    public class GameRandomIdDeterminismTest
    {
        [Test]
        public void 同じ乱数状態からのID採番は一致する()
        {
            GameRandom.Reseed(99UL);
            var block1 = BlockInstanceId.Create();
            var car1 = TrainCarInstanceId.Create();
            var unit1 = TrainUnitInstanceId.Create();

            GameRandom.Reseed(99UL);
            Assert.AreEqual(block1, BlockInstanceId.Create(), "BlockInstanceId が乱数状態に従っていない");
            Assert.AreEqual(car1, TrainCarInstanceId.Create(), "TrainCarInstanceId が乱数状態に従っていない");
            Assert.AreEqual(unit1, TrainUnitInstanceId.Create(), "TrainUnitInstanceId が乱数状態に従っていない");
        }
    }
}
