using System;
using System.Collections.Generic;

namespace Core.Update
{
    public static class GameUpdater
    {
        private static readonly List<IUpdatable> Updates = new();
        private static double _updateMillSecondTime;

        private static DateTime _prevUpdateDateTime = DateTime.Now;

        [Obsolete("")] public static double UpdateMillSecondTime => _updateMillSecondTime;

        //TODO Disposable
        public static void RegisterUpdater(IUpdatable iUpdatable)
        {
            Updates.Add(iUpdatable);
        }

        public static void Update()
        {
            _updateMillSecondTime = DateTime.Now.Subtract(_prevUpdateDateTime).TotalMilliseconds;
            _prevUpdateDateTime = DateTime.Now;
            
            for (var i = Updates.Count - 1; 0 <= i; i--) Updates[i]?.Update();

            //100
            while (DateTime.Now.Subtract(_prevUpdateDateTime).TotalMilliseconds <= 100)
            {
            }
        }
    }
}