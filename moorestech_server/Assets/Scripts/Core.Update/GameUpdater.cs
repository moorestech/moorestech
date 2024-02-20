using System;
using UniRx;
using UnityEngine;

namespace Core.Update
{
    public static class GameUpdater
    {
        private static double _updateMillSecondTime;
        private static readonly Subject<Unit> UpdateSubject = new();
        public static IObservable<Unit> UpdateObservable => UpdateSubject;

        [Obsolete("いつかアップデートシステム自体をリファクタしたい")] public static double UpdateMillSecondTime => _updateMillSecondTime;
        
        static DateTime _lastUpdateTime;

        public static void Update()
        {
            _updateMillSecondTime = (DateTime.Now - _lastUpdateTime).TotalMilliseconds;
            
            //アップデートの実行
            UpdateSubject.OnNext(Unit.Default);
            _lastUpdateTime = DateTime.Now;
        }

        public static void Dispose()
        {
            UpdateSubject.Dispose();
        }
    }
}