using System.Linq;
using Client.Game.InGame.BlockSystem.StateProcessor;
using Game.Gear.Common;
using Mooresmaster.Model.BlocksModule;
using TMPro;
using UnityEngine;

namespace Client.Game.InGame.UI.Inventory.Block
{
    public class GearMinerBlockInventoryView : MinerBlockInventoryView 
    {
        [SerializeField] private TMP_Text torque;
        [SerializeField] private TMP_Text rpm;
        [SerializeField] private TMP_Text networkInfo;
        
        private new void Update()
        {
            base.Update();
            var state = BlockGameObject.GetStateDetail<GearStateDetail>(GearStateDetail.BlockStateDetailKey);
            if (state == null)
            {
                Debug.LogError("Failed to get CommonMachineBlockStateDetail.");
                return;
            }
            
            var masterParam = (IGearMachineParam)BlockGameObject.BlockMasterElement.BlockParam;
            GearMachineBlockInventoryView.SetGearText(masterParam, state, torque, rpm, networkInfo);
        }
    }
}