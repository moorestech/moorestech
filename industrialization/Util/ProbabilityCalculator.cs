using System;

namespace industrialization.Util
{
    public class ProbabilityCalculator
    {
        public static bool DetectFromPercent(double percent)
        {
            percent *= 100;
            //小数点以下の桁数
            int digitNum = 0;
            if(percent.ToString().IndexOf(".") > 0){
                digitNum = percent.ToString ().Split(".")[1].Length;
            }

            //小数点以下を無くすように乱数の上限と判定の境界を上げる
            int rate = (int)Math.Pow (10, digitNum);

            //乱数の上限と真と判定するボーダーを設定
            int randomValueLimit = 100 * rate;
            int border = (int)(rate * percent);
            Random r = new System.Random();
            return r.Next(0, randomValueLimit) < border;
        }
    }
}