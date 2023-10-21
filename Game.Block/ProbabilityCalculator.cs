using System;

namespace Game.Block
{
    public static class ProbabilityCalculator
    {
        public static bool DetectFromPercent(double percent)
        {
            percent *= 100;
            
            var digitNum = 0;

            
            var rate = (int)Math.Pow(10, digitNum);

            
            var randomValueLimit = 100 * rate;
            var border = (int)(rate * percent);
            var r = new Random();
            return r.Next(0, randomValueLimit) < border;
        }
    }
}