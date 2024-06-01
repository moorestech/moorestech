using Game.Block;
using NUnit.Framework;

namespace Tests.UnitTest.Core.Other
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
            var trueCnt = 0;
            for (var i = 0; i < 10000; i++)
                if (ProbabilityCalculator.DetectFromPercent(percent))
                    trueCnt++;
            
            var truePercent = trueCnt / 10000.0;
            Assert.True(percent - 0.5 < truePercent);
            Assert.True(truePercent < percent + 0.5);
        }
    }
}