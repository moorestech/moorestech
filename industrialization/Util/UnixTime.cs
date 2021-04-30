using System;

namespace industrialization.Util
{
    public static class UnixTime
    {  
        private static DateTime UNIX_EPOCH =
            new DateTime(1970, 1, 1, 0, 0, 0, 0);

        //TODO このメソッド、クラスを消す
        public static long GetNowUnixTime()
        {
            DateTime targetTime = DateTime.Now;
            // UTC時間に変換
            targetTime = targetTime.ToUniversalTime();

            // UNIXエポックからの経過時間を取得
            TimeSpan elapsedTime = targetTime - UNIX_EPOCH;
   
            // 経過秒数に変換
            return (long)elapsedTime.TotalSeconds;
        }
    }
}