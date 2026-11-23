using Game.Block.Interface.Component;
using NUnit.Framework;
using Core.Master;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Game.CleanRoomYield
{
    public class CleanRoomClassYieldTest
    {
        [Test]
        public void FromThresholdIndexCarriesTheRowValuesAndIndexTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // テストマスタのクラスC（添字2）の値と行を運ぶ
            // Carries class C's values and row from the test master
            var classC = CleanRoomClassYield.FromThresholdIndex(2);
            var row = MasterHolder.CleanRoomMaster.Thresholds[2];
            Assert.AreEqual(2, classC.ThresholdIndex);
            Assert.AreEqual(row.QualityMultiplier, classC.QualityMultiplier, 1e-6f);
            Assert.AreEqual(row.LayerFailRate, classC.LayerFailRate, 1e-6f);
            Assert.AreEqual(row.DieLossRate, classC.DieLossRate, 1e-6f);
        }

        [Test]
        public void PromotionTargetIsTheRowAboveExceptAtTheBestRowTest()
        {
            // いちばん良い行（添字0）には昇格の目標が無い
            // The best row (index 0) has no promotion target
            Assert.IsFalse(CleanRoomClassYield.TryGetPromotionTargetIndex(0, out _));

            // Out（行数4）はいちばん下の行、行1は行0へ昇格する
            // Out (row count 4) promotes to the last row; row 1 promotes to row 0
            Assert.IsTrue(CleanRoomClassYield.TryGetPromotionTargetIndex(4, out var fromOut));
            Assert.AreEqual(3, fromOut);
            Assert.IsTrue(CleanRoomClassYield.TryGetPromotionTargetIndex(1, out var fromRowOne));
            Assert.AreEqual(0, fromRowOne);
        }
    }
}
