using System;
using UniRx;
using UnityEngine;

namespace MainGame.UnityView.UI.Mission
{
    public abstract class MissionBase
    {
        public int Priority { get; }
        public bool IsDone { get; private set; }
        public string MissionNameKey { get; }
        public IObservable<Unit> OnDone => _onDone;
        private readonly Subject<Unit> _onDone = new();

        protected abstract void IfNotDoneUpdate();

        public void Update()
        {
            if (!IsDone)
            {
                IfNotDoneUpdate();
            }
        }
        
        
        protected MissionBase(int priority, string missionNameKey)
        {
            Priority = priority;
            IsDone = PlayerPrefs.GetInt(missionNameKey, 0) == 1;
            MissionNameKey = missionNameKey;
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