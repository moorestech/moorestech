using System;
using System.Threading;
using UniRx;
using UnityEngine;

namespace Core.Update
{
    public static class GameUpdater
    {
        private static readonly Subject<Unit> UpdateSubject = new();
        public static IObservable<Unit> UpdateObservable => UpdateSubject;
        
        [Obsolete("いつかアップデートシステム自体をリファクタしたい")] public static double UpdateMillSecondTime { get; private set; } = 0;
        
        static DateTime _lastUpdateTime = DateTime.Now;

        public static void Update()
        {
            //アップデートの実行
            UpdateSubject.OnNext(Unit.Default);
            
            UpdateMillSecondTime = (DateTime.Now - _lastUpdateTime).TotalMilliseconds;
            _lastUpdateTime = DateTime.Now;
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

        public static void Dispose()
        {
            UpdateSubject.Dispose();
        }
    }
}