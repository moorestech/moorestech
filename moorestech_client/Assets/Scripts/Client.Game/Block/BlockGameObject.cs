using System.Collections.Generic;
using Client.Common;
using Client.Game.BlockSystem.StateChange;
using Game.Block.Interface.BlockConfig;
using UnityEngine;

namespace Client.Game.Block
{
    public class BlockGameObject : MonoBehaviour
    {
        public int BlockId { get; private set; }
        public BlockConfigData BlockConfig { get; private set; }
        public Vector3Int BlockPosition { get; private set; } = Vector3Int.zero;
        public IBlockStateChangeProcessor BlockStateChangeProcessor { get; private set; }
        
        private List<RendererMaterialReplacer> _rendererMaterialReplacer;

        public void Initialize(BlockConfigData blockConfig, Vector3Int position, IBlockStateChangeProcessor blockStateChangeProcessor)
        {
            BlockPosition = position;
            BlockId = blockConfig.BlockId;
            BlockConfig = blockConfig;
            BlockStateChangeProcessor = blockStateChangeProcessor;

            foreach (var child in gameObject.GetComponentsInChildren<BlockGameObjectChild>()) child.Init(this);
            
            _rendererMaterialReplacer = new List<RendererMaterialReplacer>();
            foreach (var renderer in GetComponentsInChildren<Renderer>()) _rendererMaterialReplacer.Add(new RendererMaterialReplacer(renderer));
        }
        
        public void SetRemovePreviewMaterial()
        {
            var placePreviewMaterial = Resources.Load<Material>(MaterialConst.PreviewRemoveBlockMaterial);
            foreach (var replacer in _rendererMaterialReplacer) replacer.SetMaterial(placePreviewMaterial);
        }
        
        public void ResetMaterial()
        {
            foreach (var replacer in _rendererMaterialReplacer) replacer.ResetMaterial();
        }
    }
}