using Game.CleanRoom.Util;
using NUnit.Framework;

namespace Tests.UnitTest.Game
{
    public class CleanRoomPurityLogicTest
    {
        private static readonly double[] RequiredAirChangeRates = { 0.0167, 0.0083, 0.0042, 0.0014 };

        [Test]
        public void InsufficientAirChangeRateIsOutTest()
        {
            // どの行の必要換気回数にも届かなければ Out（行数）になる
            // Falling short of every row's required rate yields Out (the row count)
            var index = CleanRoomPurityLogic.DecideThresholdIndex(4, 0.0010, RequiredAirChangeRates);
            Assert.AreEqual(4, index);
        }

        [Test]
        public void PromotionRequiresOnePointTwoFiveTimesRequiredRateTest()
        {
            // Out(4) から B(1) へ: 0.0083 ちょうどでは届かず C(2) 止まり、0.0083×1.25 以上で B になる
            // From Out(4) to B(1): exactly 0.0083 stops at C(2); 0.0083 x 1.25 or more reaches B
            Assert.AreEqual(2, CleanRoomPurityLogic.DecideThresholdIndex(4, 0.0083, RequiredAirChangeRates));
            Assert.AreEqual(1, CleanRoomPurityLogic.DecideThresholdIndex(4, 0.0083 * 1.25, RequiredAirChangeRates));
        }

        [Test]
        public void CurrentClassIsKeptAtExactRequiredRateTest()
        {
            // 現在のクラスは昇格マージン無しの素の必要換気回数で維持できる
            // The current class is kept at its raw required rate without the promotion margin
            Assert.AreEqual(1, CleanRoomPurityLogic.DecideThresholdIndex(1, 0.0083, RequiredAirChangeRates));
        }

        [Test]
        public void LowerAirChangeRateDemotesImmediatelyTest()
        {
            // 必要換気回数を下回ったその判定で1つ下の行へ降格する
            // Dropping below the required rate demotes to the next row in the same decision
            Assert.AreEqual(2, CleanRoomPurityLogic.DecideThresholdIndex(1, 0.0082, RequiredAirChangeRates));
        }

        [Test]
        public void ZeroVolumeAirChangeRateIsZeroTest()
        {
            // 体積0の部屋は換気回数0になり、NaN や無限大を判定へ渡さない
            // A zero-volume room yields a zero air change rate, never passing NaN or infinity to the decision
            Assert.AreEqual(0d, CleanRoomPurityLogic.AirChangeRate(5d, 0));
            Assert.AreEqual(0.5d, CleanRoomPurityLogic.AirChangeRate(5d, 10), 1e-9);
        }
    }
}
