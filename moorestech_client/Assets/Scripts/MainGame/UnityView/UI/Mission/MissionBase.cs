using System;
using UniRx;
using UnityEngine;

namespace MainGame.UnityView.UI.Mission
{
    public abstract class MissionBase
    {
        private readonly Subject<Unit> _onDone = new();


        protected MissionBase(int priority, string missionNameKey, params string[] missionNameAddContents)
        {
            Priority = priority;
            IsDone = PlayerPrefs.GetInt(missionNameKey + "_IsDone", 0) == 1;
            MissionNameKey = missionNameKey;
            MissionNameAddContents = missionNameAddContents;
        }

        public int Priority { get; }
        public bool IsDone { get; private set; }
        public string MissionNameKey { get; }
        public string[] MissionNameAddContents { get; }
        public IObservable<Unit> OnDone => _onDone;

        protected abstract void IfNotDoneUpdate();

        public void Update()
        {
            if (!IsDone) IfNotDoneUpdate();
        }

        protected void Done()
        {
            IsDone = true;
            PlayerPrefs.SetInt(MissionNameKey, 1);
            PlayerPrefs.Save();
            _onDone.OnNext(Unit.Default);
        }
    }
}