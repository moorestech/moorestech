using NUnit.Framework;
using ProbabilityCalculator = Game.Block.ProbabilityCalculator;

namespace Test.UnitTest.Core.Other
{
    public class UtilTest
    {
        //確立のテストだから1万回繰り返して平均を取る
        //+-5%なら許容範囲内
        [TestCase(0.0)]
        [TestCase(0.1)]
        [TestCase(0.2)]
        [TestCase(0.3)]
        [TestCase(0.3)]
        [TestCase(0.5)]
        [TestCase(0.6)]
        [TestCase(0.7)]
        [TestCase(0.8)]
        [TestCase(0.9)]
        [TestCase(1)]
        public void DetectFromPercentTest(double percent)
        {
            int trueCnt = 0;
            for (int i = 0; i < 10000; i++)
            {
                if (ProbabilityCalculator.DetectFromPercent(percent))
                {
                    trueCnt++;
                }
            }

            double truePercent = trueCnt / 10000.0;
            Assert.True(percent - 0.5 < truePercent);
            Assert.True(truePercent < percent + 0.5);
        }
    }
}