using NUnit.Framework;
using MapGenerator.Pipeline.Spawn;

namespace MapGenerator.Tests.EditMode.Spawn
{
    public class DistanceTransformTests
    {
        [Test]
        public void ChebyshevDistance_Center_IsFarthestFromBorder()
        {
            // 5x5 全true → 中央(2,2)が境界(外側false扱い)から最遠=2
            int w = 5, h = 5;
            var mask = new bool[w * h];
            for (int i = 0; i < mask.Length; i++) mask[i] = true;
            var dist = DistanceTransform.ChebyshevToFalse(mask, w, h);
            Assert.AreEqual(1, dist[0]);                 // 角は境界距離1
            Assert.AreEqual(3, dist[2 * w + 2]);         // 中央 (外周false→2まで、+1で3)
        }

        [Test]
        public void PoleOfInaccessibility_PicksConstrainedMaxMinCell()
        {
            // grassClearance と waterClearance を別々に与え、両制約を満たす最大minセルを選ぶ
            int w = 3, h = 1;
            var grass = new float[] { 1f, 3f, 1f }; // セル
            var water = new float[] { 3f, 2f, 3f };
            // 制約 grassMin=2, waterMin=2 → セル0(grass1)失格, セル1(min(3,2)=2)合格, セル2(grass1)失格
            int best = DistanceTransform.PickPole(grass, water, w * h,
                grassMin: 2f, waterMin: 2f);
            Assert.AreEqual(1, best);
        }

        [Test]
        public void PoleOfInaccessibility_NoCellSatisfies_ReturnsMinusOne()
        {
            var grass = new float[] { 1f, 1f };
            var water = new float[] { 1f, 1f };
            int best = DistanceTransform.PickPole(grass, water, 2, grassMin: 5f, waterMin: 5f);
            Assert.AreEqual(-1, best);
        }
    }
}
