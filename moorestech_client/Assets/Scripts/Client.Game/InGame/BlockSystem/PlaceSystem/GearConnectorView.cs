using Core.Master;
using Mooresmaster.Model.BlocksModule;
using Mooresmaster.Model.GearModule;
using Mooresmaster.Model.InventoryConnectsModule;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem
{
    public class GearConnectorView : MonoBehaviour, IPreviewOnlyObject
    {
        [SerializeField] private InventoryConnectorLineView linePrefab;
        
        
        public void Initialize(BlockId blockId)
        {
            var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(blockId);
            if (blockMaster.BlockParam is IGearConnectors gearConnectors)
            {
                SetGearConnectors(gearConnectors.Gear);
            }
            
            #region Internal
            
            void SetGearConnectors(Gear gear)
            {
                foreach (var gearConnect in gear.GearConnects.items)
                {
                    var endPos = gearConnect.Offset;
                    if (gearConnect.Directions == null) return;
                    
                    foreach (var direction in gearConnect.Directions)
                    {
                        var startPos = endPos + direction;
                        var line = Instantiate(linePrefab, transform);
                        line.SetPoints(startPos, endPos);
                    }
                }
            }
            
             #endregion
        }
        public void SetActive(bool active)
        {
            gameObject.SetActive(active);
        }
        public void SetEnableRenderers(bool enable)
        {
            gameObject.SetActive(enable);
        }
    }
}