using System;
using MainGame.UnityView.Control;
using MainGame.UnityView.UI.Mission;
using UniRx;
using UnityEngine;

namespace MainGame.Presenter.Mission.MissionImplementations
{
    public class WASDMoveMission : IMissionImplementation
    {
        public int Priority => 10000;
        public bool IsDone { get; private set; }
        public string MissionNameKey => GetType().Name;
        public IObservable<Unit> OnDone => _onDone;
        private readonly Subject<Unit> _onDone = new();


        public void Update()
        {
            if (IsDone)
            {
                return;
            }

            if (InputManager.Player.Move.GetKeyDown)
            {
                IsDone = true;
                PlayerPrefs.SetInt(GetType().Name, 1);
                PlayerPrefs.Save();
                _onDone.OnNext(Unit.Default);
            }
        }

        public WASDMoveMission()
        {
            IsDone = PlayerPrefs.GetInt(GetType().Name, 0) == 1;
        }
    }
}