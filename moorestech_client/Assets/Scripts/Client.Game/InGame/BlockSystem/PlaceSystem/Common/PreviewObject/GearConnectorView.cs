using Core.Master;
using Mooresmaster.Model.BlocksModule;
using Mooresmaster.Model.GearModule;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewObject
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
                foreach (var gearConnect in gear.GearConnects)
                {
                    var endPos = gearConnect.Offset;
                    // 方向無制限のコネクタは線を描けないが、残りのコネクタは描き続ける
                    // An unrestricted connector has no line to draw, but the remaining connectors still do
                    if (gearConnect.Directions == null) continue;
                    
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