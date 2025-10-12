using System;
using System.Threading;
using UniRx;

namespace Core.Update
{
    public static class GameUpdater
    {
        public static IObservable<Unit> UpdateObservable => _updateSubject;
        private static Subject<Unit> _updateSubject = new();
        
        public static IObservable<Unit> LateUpdateObservable => _lateUpdateSubject;
        private static Subject<Unit> _lateUpdateSubject = new();
        
        private static DateTime _lastUpdateTime = DateTime.Now;
        
        [Obsolete("いつかアップデートシステム自体をリファクタしたい")] public static double UpdateSecondTime { get; private set; }
        
        public static void Update()
        {
            //アップデートの実行
            UpdateDeltaTime();
            _updateSubject.OnNext(Unit.Default);
            _lateUpdateSubject.OnNext(Unit.Default);
        }
        
        public static void UpdateDeltaTime()
        {
            UpdateSecondTime = (DateTime.Now - _lastUpdateTime).TotalSeconds;
            _lastUpdateTime = DateTime.Now;
        }
        
        public static void ResetUpdate()
        {
            _updateSubject = new Subject<Unit>();
            _lateUpdateSubject = new Subject<Unit>();
            UpdateSecondTime = 0;
            _lastUpdateTime = DateTime.Now;
        }
        
        public static void Dispose()
        {
            _updateSubject.Dispose();
            _lateUpdateSubject.Dispose();
        }
        
#if UNITY_EDITOR
        public static void UpdateWithWait()
        {
            //TODO ゲームループ周りの修正についてはちょっと考えたい
            Update();
            Wait();
        }
        
        public static void SpecifiedDeltaTimeUpdate(double updateSecondTime)
        {
            UpdateSecondTime = updateSecondTime;
            _updateSubject.OnNext(Unit.Default);
            _lateUpdateSubject.OnNext(Unit.Default);
        }
        
        public static void Wait()
        {
            Thread.Sleep(5);
        }
#endif
    }
}