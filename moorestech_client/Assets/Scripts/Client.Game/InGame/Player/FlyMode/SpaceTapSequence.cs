namespace Client.Game.InGame.Player.FlyMode
{
    // 押下時刻の列から連続回数を数える。間隔が開けば1から数え直す
    // Counts consecutive taps from press times, restarting at 1 after a long gap
    public class SpaceTapSequence
    {
        private const float MaxTapIntervalSeconds = 0.3f;

        private int _count;
        private float _lastTapTime;

        public int RegisterTap(float tapTime)
        {
            _count = _count > 0 && tapTime - _lastTapTime <= MaxTapIntervalSeconds ? _count + 1 : 1;
            _lastTapTime = tapTime;
            return _count;
        }

        public void Reset()
        {
            _count = 0;
        }
    }
}
