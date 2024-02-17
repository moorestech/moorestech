using System;
using UniRx;

namespace Core.Update
{
    public static class GameUpdater
    {
        private static double _updateMillSecondTime;
        private static readonly Subject<Unit> UpdateSubject = new();
        public static IObservable<Unit> UpdateObservable => UpdateSubject;

        [Obsolete("いつかアップデートシステム自体をリファクタしたい")] public static double UpdateMillSecondTime => _updateMillSecondTime;

        public static void Update()
        {
            //アップデートの実行
            UpdateSubject.OnNext(Unit.Default);
        }

        public static void Dispose()
        {
            UpdateSubject.Dispose();
        }
    }
}