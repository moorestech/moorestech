using System.Linq;
using Client.Game.InGame.BlockSystem.StateProcessor;
using Mooresmaster.Model.BlocksModule;
using TMPro;
using UnityEngine;

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
            // ここが重かったら検討
            var processor = (GearStateChangeProcessor)BlockGameObject.BlockStateChangeProcessors.FirstOrDefault(x => x as GearStateChangeProcessor);
            if (processor == null)
            {
                Debug.LogError("GearStateChangeProcessorがアタッチされていません。");
                return;
            }
            
            var masterParam = (GearMachineBlockParam)BlockGameObject.BlockMasterElement.BlockParam;
            var requireTorque = masterParam.RequireTorque;
            var requireRpm = masterParam.RequiredRpm;
            
            var currentTorque = processor.CurrentGearState?.CurrentTorque ?? 0;
            var currentRpm = processor.CurrentGearState?.CurrentRpm ?? 0;
            
            torque.text = $"トルク: {currentTorque:F2} / {requireTorque:F2}";
            if (currentTorque < requireTorque)
            {
                torque.text = $"トルク: <color=red>{currentTorque:F2}</color> / {requireTorque:F2}";
            }
            
            rpm.text = $"回転数: {currentRpm:F2} / {requireRpm:F2}";
            if (currentRpm < requireRpm)
            {
                rpm.text = $"回転数: <color=red>{currentRpm:F2}</color> / {requireRpm:F2}";
            }
            
            var rate = processor.CurrentGearState?.GearNetworkOperatingRate ?? 0;
            var requiredPower = processor.CurrentGearState?.GearNetworkTotalRequiredPower ?? 0;
            var generatePower = processor.CurrentGearState?.GearNetworkTotalGeneratePower ?? 0;
            networkInfo.text = $"歯車ネットワーク情報 稼働率: {rate * 100:F2}% 必要力: {requiredPower:F2} 生成力: {generatePower:F2}";
        }
    }
}