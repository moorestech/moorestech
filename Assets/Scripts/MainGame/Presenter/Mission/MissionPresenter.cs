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


        public void Initialize(ContainerBuilder containerBuilder)
        {
            var missionList = new List<IMissionImplementation>();
            missionList.Add(new WASDMoveMission());
            
            missionUIController.SetMissionList(missionList);
        }
        
        
    }
}