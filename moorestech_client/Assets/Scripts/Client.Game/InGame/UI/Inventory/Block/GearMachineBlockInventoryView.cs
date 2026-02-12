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
                Debug.LogError("Failed to get CommonMachineBlockStateDetail.");
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
            
            torqueText.text = $"Torque: {currentTorque:F2} / {requireTorque:F2}";
            if (currentTorque < requireTorque)
            {
                torqueText.text = $"Torque: <color=red>{currentTorque:F2}</color> / {requireTorque:F2}";
            }
            
            rpmText.text = $"RPM: {currentRpm:F2} / {requireRpm:F2}";
            if (currentRpm < requireRpm)
            {
                rpmText.text = $"RPM: <color=red>{currentRpm:F2}</color> / {requireRpm:F2}";
            }
            
            var rate = state.GearNetworkOperatingRate;
            var requiredPower = state.GearNetworkTotalRequiredPower;
            var generatePower = state.GearNetworkTotalGeneratePower;
            
            networkInfoText.text = $"{GetStopReasonText(state.StopReason)} Required power: {requiredPower:F2} Generated power: {generatePower:F2}";
        }
    }
}