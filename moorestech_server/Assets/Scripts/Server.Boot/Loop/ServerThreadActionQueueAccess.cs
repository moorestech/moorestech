namespace Server.Boot.Loop
{
    // 現サーバーのキューへの唯一の入口。キュー状態は各インスタンスが持ち、ここは参照1本だけを持つ
    // The single entry point to the current server's queue; queue state lives in each instance and only the reference is held here
    public static class ServerThreadActionQueueAccess
    {
        private static readonly object Gate = new();
        private static ServerThreadActionQueue _current;

        public static ServerThreadActionQueue Current
        {
            get { lock (Gate) return _current; }
        }

        public static void SetCurrent(ServerThreadActionQueue queue)
        {
            lock (Gate) _current = queue;
        }

        // 自分が現役のときだけ参照を降ろす。古いサーバーの終了で新しいサーバーの入口を閉じない
        // Withdraw the reference only while still current, so an old server's shutdown never closes a newer server's entry
        public static void ClearCurrent(ServerThreadActionQueue queue)
        {
            lock (Gate)
            {
                if (_current != queue) return;
                _current = null;
            }
        }
    }
}
