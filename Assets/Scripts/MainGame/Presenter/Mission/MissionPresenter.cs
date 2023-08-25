using System;
using System.Collections.Generic;
using MainGame.Presenter.Mission.Missions;
using MainGame.UnityView.Control;
using MainGame.UnityView.UI.Mission;
using UnityEngine;
using VContainer;

namespace MainGame.Presenter.Mission
{
    public class MissionPresenter : MonoBehaviour
    {
        [SerializeField] private MissionUIController missionUIController;

        private readonly List<MissionBase> _missionList = new();

        public void Initialize(ContainerBuilder containerBuilder)
        {
            //TODO ミッションリストを外から追加できるようにする
            _missionList.Add(new InputKeyMission(10000,"WASDMoveMission",InputManager.Player.Move));
            _missionList.Add(new InputKeyMission(9999,"OpenInventoryMission",InputManager.UI.OpenInventory));

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