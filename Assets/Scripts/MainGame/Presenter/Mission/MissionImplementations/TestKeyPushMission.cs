using System;
using MainGame.UnityView.UI.Mission;
using UniRx;
using UnityEngine;

namespace MainGame.Presenter.Mission.MissionImplementations
{
    public class TestKeyPushMission : IMissionImplementation
    {
        private readonly KeyCode _keyCode;
        
        public TestKeyPushMission(int priority,KeyCode keyCode)
        {
            Priority = priority;
            _keyCode = keyCode;
            MissionNameKey = keyCode.ToString();
        }

        public int Priority { get; }
        public bool IsDone { get; private set; }
        public string MissionNameKey { get; }
        
        public IObservable<Unit> OnDone => _onDone;
        private readonly Subject<Unit> _onDone = new();
        
        public void Update()
        {
            if (IsDone)
            {
                return;
            }

            if (Input.GetKeyDown(_keyCode))
            {
                IsDone = true;
                _onDone.OnNext(Unit.Default);
            }
        }
    }
}