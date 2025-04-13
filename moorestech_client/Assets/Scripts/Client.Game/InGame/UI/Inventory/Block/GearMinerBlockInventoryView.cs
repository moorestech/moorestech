using System.Linq;
using Client.Game.InGame.BlockSystem.StateProcessor;
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
            // ここが重かったら検討
            var processor = (GearStateChangeProcessor)BlockGameObject.BlockStateChangeProcessors.FirstOrDefault(x => x as GearStateChangeProcessor);
            if (processor == null)
            {
                Debug.LogError("GearStateChangeProcessorがアタッチされていません。");
                return;
            }
            
            var masterParam = (IGearMachineParam)BlockGameObject.BlockMasterElement.BlockParam;
            GearMachineBlockInventoryView.SetGearText(masterParam, processor, torque, rpm, networkInfo);
        }
    }
}