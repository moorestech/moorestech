using Client.Common;
using Game.Block.Interface.BlockConfig;
using UnityEngine;
using UnityEngine.VFX;

namespace Client.Game.Block
{
    public class BlockPreviewObject : MonoBehaviour
    {
        public BlockConfigData BlockConfig { get; private set; }

        public void Initialize(BlockConfigData blockConfigData)
        {
            BlockConfig = blockConfigData;

            var previewMaterial = Resources.Load<Material>(MaterialConst.PreviewPlaceBlockMaterial);
            Renderer[] blockRenderer = GetComponentsInChildren<Renderer>();
            foreach (var renderer in blockRenderer)
            {
                var replacer = new RendererMaterialReplacer(renderer);
                replacer.SetMaterial(previewMaterial);
            }
            
            var visualEffects = GetComponentsInChildren<VisualEffect>(true);
            foreach (var visualEffect in visualEffects)
            {
                visualEffect.gameObject.SetActive(false);
            }
        }
    }
}