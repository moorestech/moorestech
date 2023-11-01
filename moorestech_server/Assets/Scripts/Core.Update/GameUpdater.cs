using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Core.Update
{
    public static class GameUpdater
    {
        private static readonly List<IUpdatable> Updates = new();
        private static double _updateMillSecondTime;

        private static DateTime _prevUpdateDateTime = DateTime.Now;

        [Obsolete("いつかアップデートシステム自体をリファクタしたい")] public static double UpdateMillSecondTime => _updateMillSecondTime;

        //TODO これをDisposableにする
        public static void RegisterUpdater(IUpdatable iUpdatable)
        {
            Updates.Add(iUpdatable);
        }

        public static void Update()
        {
            _updateMillSecondTime = DateTime.Now.Subtract(_prevUpdateDateTime).TotalMilliseconds;
            _prevUpdateDateTime = DateTime.Now;
            //アップデートの実行
            for (var i = Updates.Count - 1; 0 <= i; i--) Updates[i]?.Update();

            //次のアップデートと最低100ミリ秒間隔を開けて実行する
            while (DateTime.Now.Subtract(_prevUpdateDateTime).TotalMilliseconds <= 100)
            {
            }
        }
    }
}