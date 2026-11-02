using System;
using UniRx;

namespace Game.Orbit
{
    public class RocketLaunchHistory
    {
        public bool HasLaunchedOnce { get; private set; }
        public IObservable<Unit> OnFirstLaunch => _onFirstLaunch;

        private readonly Subject<Unit> _onFirstLaunch = new();

        public void RecordLaunch()
        {
            // 通知はワールドで最初の1回だけ。2回目以降は状態が変わらない
            // Notify only on the world's first launch; later launches change nothing
            if (HasLaunchedOnce) return; // silent-ok: 初回後の記録は冪等 / 呼び出し元はHasLaunchedOnceで確認できる
            HasLaunchedOnce = true;
            _onFirstLaunch.OnNext(Unit.Default);
        }

        public void LoadSaveData(bool hasLaunchedOnce)
        {
            HasLaunchedOnce = hasLaunchedOnce;
        }
    }
}
