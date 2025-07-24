using Core.Master;
using Mooresmaster.Model.BlocksModule;
using Mooresmaster.Model.InventoryConnectsModule;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem
{
    public class InventoryConnectorView : MonoBehaviour, IPreviewOnlyObject
    {
        [SerializeField] private InventoryConnectorLineView linePrefab;
        
        
        public void Initialize(BlockId blockId)
        {
            var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(blockId);
            if (blockMaster.BlockParam is IInventoryConnectors inventoryConnectors)
            {
                SetInventoryConnectors(inventoryConnectors.InventoryConnectors);
            }
            
            #region Internal
            
            void SetInventoryConnectors(InventoryConnects inventory)
            {
                foreach (var inputConnect in inventory.InputConnects.items)
                {
                    var endPos = inputConnect.Offset;
                    if (inputConnect.Directions == null) return;
                    
                    foreach (var direction in inputConnect.Directions)
                    {
                        var startPos = endPos + direction;
                        var line = Instantiate(linePrefab, transform);
                        line.SetPoints(startPos, endPos);
                    }
                }
                
                foreach (var outputConnect in inventory.OutputConnects.items)
                {
                    var startPos = outputConnect.Offset;
                    if (outputConnect.Directions == null) return;
                    
                    foreach (var direction in outputConnect.Directions)
                    {
                        var endPos = startPos + direction;
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