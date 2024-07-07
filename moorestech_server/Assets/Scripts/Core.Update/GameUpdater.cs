using System;
using System.Threading;
using UniRx;
using UnityEngine;

namespace Core.Update
{
    public static class GameUpdater
    {
        private static Subject<Unit> _updateSubject = new();
        
        private static DateTime _lastUpdateTime = DateTime.Now;
        public static IObservable<Unit> UpdateObservable => _updateSubject;
        
        [Obsolete("いつかアップデートシステム自体をリファクタしたい")] public static double UpdateMillSecondTime { get; private set; }
        
        public static void Update()
        {
            //アップデートの実行
            UpdateMillSecondTime = (DateTime.Now - _lastUpdateTime).TotalMilliseconds;
            _lastUpdateTime = DateTime.Now;
            
            _updateSubject.OnNext(Unit.Default);
        }
        
        public static void ResetUpdate()
        {
            _updateSubject = new Subject<Unit>();
        }
        
        public static void ResetTime()
        {
            _lastUpdateTime = DateTime.Now;
            UpdateMillSecondTime = 0;
        }
        
        public static void Dispose()
        {
            _updateSubject.Dispose();
        }
        
#if UNITY_EDITOR
        public static void UpdateWithWait()
        {
            //TODO ゲームループ周りの修正についてはちょっと考えたい
            Update();
            Thread.Sleep(5);
        }
#endif
    }
}