using System;
using UniRx;

namespace Core.Update
{
    public static class GameUpdater
    {
        private static double _updateMillSecondTime;
        private static readonly Subject<Unit> UpdateSubject = new();
        private static DateTime _prevUpdateDateTime = DateTime.Now;
        public static IObservable<Unit> UpdateObservable => UpdateSubject;

        [Obsolete("いつかアップデートシステム自体をリファクタしたい")] public static double UpdateMillSecondTime => _updateMillSecondTime;

        public static void Update()
        {
            _updateMillSecondTime = DateTime.Now.Subtract(_prevUpdateDateTime).TotalMilliseconds;
            _prevUpdateDateTime = DateTime.Now;
            //アップデートの実行
            UpdateSubject.OnNext(Unit.Default);

            //次のアップデートと最低100ミリ秒間隔を開けて実行する
            while (DateTime.Now.Subtract(_prevUpdateDateTime).TotalMilliseconds <= 100)
            {
            }
        }

        public static void Dispose()
        {
            UpdateSubject.Dispose();
        }
    }
}