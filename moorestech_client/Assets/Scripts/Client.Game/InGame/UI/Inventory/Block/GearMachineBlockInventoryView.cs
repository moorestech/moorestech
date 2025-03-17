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
        
        private void Update()
        {
            // ここが重かったら検討
            var processor = (GearStateChangeProcessor)BlockGameObject.BlockStateChangeProcessors.FirstOrDefault(x => x as GearStateChangeProcessor);
            if (processor == null) return;
            
            var masterParam = (GearMachineBlockParam)BlockGameObject.BlockMasterElement.BlockParam;
            var requireTorque = masterParam.RequireTorque;
            var requireRpm = masterParam.RequiredRpm;
            
            var currentTorque = processor.CurrentGearState.CurrentTorque;
            var currentRpm = processor.CurrentGearState.CurrentRpm;
            
            torque.text = $"トルク: {currentTorque} / {requireTorque}";
            if (currentTorque < requireTorque)
            {
                torque.text = $"トルク: <color=red>{currentTorque}</color> / {requireTorque}";
            }
            
            rpm.text = $"回転数: {currentRpm} / {requireRpm}";
            if (currentRpm < requireRpm)
            {
                rpm.text = $"回転数: <color=red>{currentRpm}</color> / {requireRpm}";
            }
        }
    }
}