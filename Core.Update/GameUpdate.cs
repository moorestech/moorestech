using System;
using System.Collections.Generic;

namespace Core.Update
{
    public static class GameUpdate
    {
        private static readonly List<IUpdate> Updates = new List<IUpdate>();

        public static double UpdateMillSecondTime => _updateMillSecondTime;
        private static double _updateMillSecondTime = 0;

        public static void AddUpdateObject(IUpdate iUpdate)
        {
            Updates.Add(iUpdate);
        }

        private static DateTime _prevUpdateDateTime = DateTime.Now;

        public static void Update()
        {
            _updateMillSecondTime = DateTime.Now.Subtract(_prevUpdateDateTime).TotalMilliseconds;
            _prevUpdateDateTime = DateTime.Now;
            //アップデートの実行
            for (int i = Updates.Count - 1; 0 <= i; i--)
            {
                Updates[i]?.Update();
            }

            //次のアップデートと最低10ミリ秒間隔を開けて実行する
            while (DateTime.Now.Subtract(_prevUpdateDateTime).TotalMilliseconds <= 10)
            {
            }
        }
    }
}