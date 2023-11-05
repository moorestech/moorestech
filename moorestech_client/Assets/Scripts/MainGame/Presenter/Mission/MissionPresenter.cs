using System.Collections.Generic;
using Core.Item.Config;
using MainGame.Presenter.Inventory.Receive;
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


        private void Update()
        {
            foreach (var mission in _missionList) mission.Update();
        }

        public void Initialize(IObjectResolver resolver)
        {
            var itemConfig = resolver.Resolve<IItemConfig>();
            var mainInventoryPresenter = resolver.Resolve<MainInventoryViewPresenter>();

            //TODO ミッションリストを外から追加できるようにする
            _missionList.Add(new InputKeyMission(10000, "WASDMoveMission", InputManager.Player.Move));
            _missionList.Add(new InputKeyMission(9990, "OpenInventoryMission", InputManager.UI.OpenInventory));
            _missionList.Add(new GetItemMission(9980, "stone", 3, "GetStoneMission", itemConfig, mainInventoryPresenter));
            _missionList.Add(new GetItemMission(9980, "stone tool", 1, itemConfig, mainInventoryPresenter));
            _missionList.Add(new GetItemMission(9980, "simple ax", 1, itemConfig, mainInventoryPresenter));
            _missionList.Add(new GetItemMission(9980, "iron ore", 1, itemConfig, mainInventoryPresenter));
            _missionList.Add(new GetItemMission(9980, "stone smelter", 1, itemConfig, mainInventoryPresenter));
            _missionList.Add(new GetItemMission(9980, "iron ingot", 1, itemConfig, mainInventoryPresenter));
            _missionList.Add(new GetItemMission(9980, "wooden miner", 1, itemConfig, mainInventoryPresenter));
            _missionList.Add(new GetItemMission(9980, "belt conveyor", 1, itemConfig, mainInventoryPresenter));

            missionUIController.SetMissionList(_missionList);
        }
    }
}