using System.Linq;
using Client.Game.InGame.BlockSystem.StateProcessor;
using Game.Gear.Common;
using Mooresmaster.Model.BlocksModule;
using TMPro;
using UnityEngine;
using static Client.Game.InGame.UI.Inventory.Block.GearEnergyTransformerUIView;

namespace Client.Game.InGame.UI.Inventory.Block
{
    public class GearMachineBlockInventoryView : MachineBlockInventoryView
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
                Debug.LogError("CommonMachineBlockStateDetailが取得できません。");
                return;
            }
            
            var masterParam = (GearMachineBlockParam)BlockGameObject.BlockMasterElement.BlockParam;
            SetGearText(masterParam, state, torque, rpm, networkInfo);
        }
        
        public static void SetGearText(IGearMachineParam param, GearStateDetail state, TMP_Text torqueText, TMP_Text rpmText, TMP_Text networkInfoText)
        {
            var requireTorque = param.RequireTorque;
            var requireRpm = param.RequiredRpm;
            
            var currentTorque = state.CurrentTorque;
            var currentRpm = state.CurrentRpm;
            
            torqueText.text = $"トルク: {currentTorque:F2} / {requireTorque:F2}";
            if (currentTorque < requireTorque)
            {
                torqueText.text = $"トルク: <color=red>{currentTorque:F2}</color> / {requireTorque:F2}";
            }
            
            rpmText.text = $"回転数: {currentRpm:F2} / {requireRpm:F2}";
            if (currentRpm < requireRpm)
            {
                rpmText.text = $"回転数: <color=red>{currentRpm:F2}</color> / {requireRpm:F2}";
            }
            
            var rate = state.GearNetworkOperatingRate;
            var requiredPower = state.GearNetworkTotalRequiredPower;
            var generatePower = state.GearNetworkTotalGeneratePower;
            
            networkInfoText.text = $"{GetStopReasonText(state.StopReason)} 必要力: {requiredPower:F2} 生成力: {generatePower:F2}";
        }
    }
}