using Core.Master;
using Mooresmaster.Model.BlocksModule;
using Mooresmaster.Model.InventoryConnectsModule;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewObject
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
                // スキーマ上optionalのため未定義の接続配列は空扱い
                // Treat connect arrays omitted in master data as empty (optional in schema)
                foreach (var inputConnect in inventory.InputConnects ?? System.Array.Empty<InputConnectsElement>())
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
                
                foreach (var outputConnect in inventory.OutputConnects ?? System.Array.Empty<OutputConnectsElement>())
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