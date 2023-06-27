using System;
using UniRx;

namespace MainGame.UnityView.UI.Mission
{
    public interface IMissionImplementation
    {
        public int Priority { get; }
        public bool IsDone { get; }
        public string MissionNameKey { get; }
        public IObservable<Unit> OnDone { get; }

        public void Update();
    }
}