using System;
using System.Collections.Generic;

namespace Core.Update
{
    public static class GameUpdate
    {
        private static readonly List<IUpdatable> Updates = new List<IUpdatable>();

        [Obsolete("いつかアップデートシステム自体をリファクタしたい")]
        public static double UpdateMillSecondTime => _updateMillSecondTime;
        private static double _updateMillSecondTime = 0;

        public static void AddUpdateObject(IUpdatable iUpdatable)
        {
            Updates.Add(iUpdatable);
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