using System;
using UniRx;

namespace Game.Orbit
{
    // ワールドで初打ち上げが済んだか。済むまで全ての発射台は建造と発射の合図を待つ
    // Whether the world has had its first launch; until then every launch pad waits for build and launch commands
    public interface IRocketLaunchHistory
    {
        bool HasLaunchedOnce { get; }
        IObservable<Unit> OnFirstLaunch { get; }

        void RecordLaunch();
        void LoadSaveData(bool hasLaunchedOnce);
    }
}
