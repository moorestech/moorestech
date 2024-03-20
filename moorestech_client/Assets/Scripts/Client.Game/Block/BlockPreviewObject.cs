using System.Collections.Generic;
using Client.Common;
using Game.Block.Interface.BlockConfig;
using UnityEngine;

namespace Client.Game.Block
{
    public class BlockPreviewObject : MonoBehaviour
    {

        public BlockConfigData BlockConfig { get; private set; }
        
        public void Initialize(BlockConfigData blockConfigData)
        {
            BlockConfig = blockConfigData;
            
            var previewMaterial = Resources.Load<Material>(MaterialConst.PreviewPlaceBlockMaterial);
            var blockRenderer = GetComponentsInChildren<Renderer>();
            foreach (var renderer in blockRenderer)
            {
                var replacer = new RendererMaterialReplacer(renderer);
                replacer.SetMaterial(previewMaterial);
            }
        }
    }
}