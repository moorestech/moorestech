using System;
using System.Threading;
using UniRx;

namespace Core.Update
{
    public static class GameUpdater
    {
        public static IObservable<Unit> UpdateObservable => UpdateSubject;
        private static readonly Subject<Unit> UpdateSubject = new();

        private static DateTime _lastUpdateTime = DateTime.Now;

        [Obsolete("いつかアップデートシステム自体をリファクタしたい")] public static double UpdateMillSecondTime { get; private set; }

        public static void Update()
        {
            //アップデートの実行
            UpdateSubject.OnNext(Unit.Default);

            UpdateMillSecondTime = (DateTime.Now - _lastUpdateTime).TotalMilliseconds;
            _lastUpdateTime = DateTime.Now;
        }

        public static void Dispose()
        {
            UpdateSubject.Dispose();
        }

#if UNITY_EDITOR
        public static void ResetUpdate()
        {
            UpdateMillSecondTime = 0;
            _lastUpdateTime = DateTime.Now;
        }
        public static void UpdateWithWait()
        {
            //TODO ゲームループ周りの修正についてはちょっと考えたい
            Update();
            Thread.Sleep(10);
        }
#endif
    }
}