using Game.Block.Interface.BlockConfig;
using UnityEngine;
using UnityEngine.VFX;

namespace Client.Game.InGame.Block
{
    public class BlockPreviewObject : MonoBehaviour
    {
        public BlockConfigData BlockConfig { get; private set; }
        
        public void Initialize(BlockConfigData blockConfigData, Material material)
        {
            BlockConfig = blockConfigData;
            
            SetMaterial(material);
            SetVfxActive(false);
        }
        
        public void SetMaterial(Material material)
        {
            Renderer[] blockRenderer = GetComponentsInChildren<Renderer>();
            foreach (var renderer in blockRenderer)
            {
                var replacer = new RendererMaterialReplacer(renderer);
                replacer.SetMaterial(material);
            }
        }
        
        private void SetVfxActive(bool isActive)
        {
            VisualEffect[] visualEffects = GetComponentsInChildren<VisualEffect>(isActive);
            foreach (var visualEffect in visualEffects)
            {
                visualEffect.gameObject.SetActive(false);
            }
        }
    }
}