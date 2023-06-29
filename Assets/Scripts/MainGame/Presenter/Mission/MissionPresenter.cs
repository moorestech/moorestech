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
            _missionList.Add(new TestKeyPushMission(900,KeyCode.G));
            _missionList.Add(new TestKeyPushMission(890,KeyCode.H));
            _missionList.Add(new TestKeyPushMission(880,KeyCode.I));
            _missionList.Add(new TestKeyPushMission(870,KeyCode.J));
            _missionList.Add(new TestKeyPushMission(860,KeyCode.K));
            _missionList.Add(new TestKeyPushMission(850,KeyCode.L));
            _missionList.Add(new TestKeyPushMission(840,KeyCode.V));
            _missionList.Add(new TestKeyPushMission(830,KeyCode.B));
            
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