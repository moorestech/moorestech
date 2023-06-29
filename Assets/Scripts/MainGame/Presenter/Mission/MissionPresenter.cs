using System;
using System.Collections.Generic;
using MainGame.Presenter.Mission.MissionImplementations;
using MainGame.UnityView.UI.Mission;
using UnityEngine;
using VContainer;

namespace MainGame.Presenter.Mission
{
    public class MissionPresenter : MonoBehaviour
    {
        [SerializeField] private MissionUIController missionUIController;

        private readonly List<IMissionImplementation> _missionList = new();

        public void Initialize(ContainerBuilder containerBuilder)
        {
            //TODO ミッションリストを外から追加できるようにする
            _missionList.Add(new WASDMoveMission());

            missionUIController.SetMissionList(_missionList);
        }


        private void Update()
        {
            foreach (var mission in _missionList)
            {
                mission.Update();
            }
        }
    }
}