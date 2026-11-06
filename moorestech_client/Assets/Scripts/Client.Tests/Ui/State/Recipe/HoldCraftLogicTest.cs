using Client.Ui.State.Recipe.Logic;
using NUnit.Framework;

namespace Client.Tests.Ui.State.Recipe
{
    public class HoldCraftLogicTest
    {
        private const double Tolerance = 1e-9;

        private static void AssertStep(double elapsed, bool didCraft, HoldCraftStep actual)
        {
            Assert.AreEqual(elapsed, actual.Elapsed, Tolerance);
            Assert.AreEqual(didCraft, actual.DidCraft);
        }

        /// <web-test file="src/features/recipe/logic/holdCraftLogic.test.ts">craftTime 未到達なら経過を積み増すだけ</web-test>
        [Test]
        public void 製作時間に届かなければ経過を積み増すだけ()
        {
            AssertStep(0.3, false, HoldCraftLogic.Advance(0, 0.3, 1));
        }

        /// <web-test file="src/features/recipe/logic/holdCraftLogic.test.ts">craftTime 到達でクラフト発火し経過を0へ戻す</web-test>
        [Test]
        public void 製作時間に届いたら発火し経過を0へ戻す()
        {
            AssertStep(0, true, HoldCraftLogic.Advance(0.8, 0.3, 1));
        }

        /// <web-test file="src/features/recipe/logic/holdCraftLogic.test.ts">ちょうど craftTime でも発火する</web-test>
        [Test]
        public void ちょうど製作時間でも発火する()
        {
            AssertStep(0, true, HoldCraftLogic.Advance(0.5, 0.5, 1));
        }

        /// <web-test file="src/features/recipe/logic/holdCraftLogic.test.ts">連続クラフト: 発火直後は0から積み増しが再開する</web-test>
        [Test]
        public void 連続クラフトは発火直後に0から積み増しを再開する()
        {
            var first = HoldCraftLogic.Advance(0.9, 0.2, 1);
            Assert.IsTrue(first.DidCraft);
            AssertStep(0.2, false, HoldCraftLogic.Advance(first.Elapsed, 0.2, 1));
        }

        /// <web-test file="src/features/recipe/logic/holdCraftLogic.test.ts">巨大 delta でも1tick最大1クラフト</web-test>
        [Test]
        public void 巨大な経過でも1フレーム最大1回()
        {
            AssertStep(0, true, HoldCraftLogic.Advance(0, 100, 1));
        }

        /// <web-test file="src/features/recipe/logic/holdCraftLogic.test.ts">経過/craftTime を 0..1 で返す</web-test>
        [Test]
        public void 経過を製作時間で割った0から1を返す()
        {
            Assert.AreEqual(0.25, HoldCraftLogic.Progress(0.5, 2), Tolerance);
        }

        /// <web-test file="src/features/recipe/logic/holdCraftLogic.test.ts">craftTime 超過は 1 でクランプ</web-test>
        [Test]
        public void 製作時間を超えたら1で止める()
        {
            Assert.AreEqual(1, HoldCraftLogic.Progress(3, 2), Tolerance);
        }

        /// <web-test file="src/features/recipe/logic/holdCraftLogic.test.ts">craftTime&lt;=0 は満杯</web-test>
        [Test]
        public void 製作時間が0以下なら満杯()
        {
            Assert.AreEqual(1, HoldCraftLogic.Progress(0, 0), Tolerance);
        }

        /// <web-test file="src/features/recipe/logic/holdCraftLogic.test.ts">正の上限値である</web-test>
        [Test]
        public void フレーム1回の上限秒は正の値()
        {
            Assert.Greater(HoldCraftLogic.MaxFrameDeltaSeconds, 0);
        }
    }
}
