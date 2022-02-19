using Microsoft.VisualStudio.TestTools.UnitTesting;
using ProbabilityCalculator = Core.Block.ProbabilityCalculator;

namespace Test.UnitTest.Core.Other
{
    [TestClass]
    public class UtilTest
    {
        //確立のテストだから1万回繰り返して平均を取る
        //+-5%なら許容範囲内
        [TestMethod]
        public void DetectFromPercentTest()
        {
            DetectFromPercentTest(0.0);
            DetectFromPercentTest(0.1);
            DetectFromPercentTest(0.2);
            DetectFromPercentTest(0.3);
            DetectFromPercentTest(0.3);
            DetectFromPercentTest(0.5);
            DetectFromPercentTest(0.6);
            DetectFromPercentTest(0.7);
            DetectFromPercentTest(0.8);
            DetectFromPercentTest(0.9);
            DetectFromPercentTest(1);
        }
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
            Assert.IsTrue(percent - 0.5 < truePercent);
            Assert.IsTrue(truePercent < percent + 0.5);
        }
    }
}