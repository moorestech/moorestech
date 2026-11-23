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
            // 届かなければOut（行数）
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
            // 現クラスは素の必要値で維持
            // The current class is kept at its raw required rate without the promotion margin
            Assert.AreEqual(1, CleanRoomPurityLogic.DecideThresholdIndex(1, 0.0083, RequiredAirChangeRates));
        }

        [Test]
        public void LowerAirChangeRateDemotesImmediatelyTest()
        {
            // 下回ったら1つ降格
            // Dropping below the required rate demotes to the next row in the same decision
            Assert.AreEqual(2, CleanRoomPurityLogic.DecideThresholdIndex(1, 0.0082, RequiredAirChangeRates));
        }

        [Test]
        public void ZeroVolumeAirChangeRateIsZeroTest()
        {
            // 体積0は換気回数0（NaN回避）
            // A zero-volume room yields a zero air change rate, never passing NaN or infinity to the decision
            Assert.AreEqual(0d, CleanRoomPurityLogic.AirChangeRate(5d, 0));
            Assert.AreEqual(0.5d, CleanRoomPurityLogic.AirChangeRate(5d, 10), 1e-9);
        }

        [Test]
        public void UnitsToPromoteCountThePromotionMarginAndLostCellsTest()
        {
            // B(1) から A(0) へは 0.0167×1.25 が要る。8台では届かず、9台で届く
            // B(1) to A(0) needs 0.0167 x 1.25; eight units fall short and nine reach it
            Assert.IsTrue(CleanRoomPurityLogic.TryCountUnitsToPromote(1, 40, 4000, 5, 2, RequiredAirChangeRates, out var units));
            Assert.AreEqual(9, units);

            // 1台が100セルなら体積の減りで7台で届く
            // With 100-cell units, shrinking volume lets seven units reach the target
            Assert.IsTrue(CleanRoomPurityLogic.TryCountUnitsToPromote(1, 40, 4000, 5, 100, RequiredAirChangeRates, out var bigUnits));
            Assert.AreEqual(7, bigUnits);
        }

        [Test]
        public void UnitsFromOutTargetTheLowestRowTest()
        {
            // Out(4) の次は D(3)。除去0・体積26に1セルの台を1台置くと5/25で届く
            // The next class from Out(4) is D(3); one 1-cell unit gives 5/25 in volume 25
            Assert.IsTrue(CleanRoomPurityLogic.TryCountUnitsToPromote(4, 0, 26, 5, 1, RequiredAirChangeRates, out var units));
            Assert.AreEqual(1, units);
        }

        [Test]
        public void UnitsAreUnavailableAtTheBestRowOrWithoutFreeCellsTest()
        {
            // いちばん良い行には次が無い
            // The best row has no next row
            Assert.IsFalse(CleanRoomPurityLogic.TryCountUnitsToPromote(0, 40, 4000, 5, 2, RequiredAirChangeRates, out _));

            // 1台で空きセルを使い切る部屋では届かない
            // A room whose free cells one unit would use up cannot reach the target
            Assert.IsFalse(CleanRoomPurityLogic.TryCountUnitsToPromote(4, 0, 10, 5, 10, RequiredAirChangeRates, out _));
        }
    }
}
