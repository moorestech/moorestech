using System;

namespace Game.Block
{
    public static class ProbabilityCalculator
    {
        public static bool DetectFromPercent(double percent)
        {
            percent *= 100;
            //小数点以下の桁数
            var digitNum = 0;
            
            //小数点以下を無くすように乱数の上限と判定の境界を上げる
            var rate = (int)Math.Pow(10, digitNum);
            
            //乱数の上限と真と判定するボーダーを設定
            var randomValueLimit = 100 * rate;
            var border = (int)(rate * percent);
            var r = new Random();
            return r.Next(0, randomValueLimit) < border;
        }
    }
}