using System;
using System.Collections.Generic;

namespace Core.GameSystem
{
    public static class GameUpdate
    {
        private static List<IUpdate> _updates = new List<IUpdate>();

        public static double UpdateTime => _updateTime;
        private static double _updateTime = 0;
        
        public static void AddUpdateObject(IUpdate iUpdate)
        {
            _updates.Add(iUpdate);
        }

        private static DateTime _prevUpdateDateTime = DateTime.Now;
        public static void Update()
        {
            _updateTime = DateTime.Now.Subtract(_prevUpdateDateTime).TotalMilliseconds;
            _prevUpdateDateTime = DateTime.Now;
            //アップデートの実行
            for (int i = _updates.Count - 1; 0 <= i ; i--)
            {
                _updates[i]?.Update();
            }
            //次のアップデートと最低10ミリ秒間隔を開けて実行する
            while (DateTime.Now.Subtract(_prevUpdateDateTime).TotalMilliseconds <= 10){}
        }
    }
}