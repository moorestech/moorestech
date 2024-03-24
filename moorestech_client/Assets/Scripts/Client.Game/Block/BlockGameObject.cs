using System.Collections.Generic;
using Client.Common;
using Client.Game.BlockSystem.StateChange;
using Cysharp.Threading.Tasks;
using Game.Block.Interface.BlockConfig;
using UnityEngine;

namespace Client.Game.Block
{
    public class BlockGameObject : MonoBehaviour
    {
        private BlockShaderAnimation _blockShaderAnimation;

        private bool _isShaderAnimationing;
        private List<RendererMaterialReplacer> _rendererMaterialReplacer;
        public int BlockId { get; private set; }
        public BlockConfigData BlockConfig { get; private set; }
        public Vector3Int BlockPosition { get; private set; } = Vector3Int.zero;
        public IBlockStateChangeProcessor BlockStateChangeProcessor { get; private set; }

        public void Initialize(BlockConfigData blockConfig, Vector3Int position, IBlockStateChangeProcessor blockStateChangeProcessor)
        {
            BlockPosition = position;
            BlockId = blockConfig.BlockId;
            BlockConfig = blockConfig;
            BlockStateChangeProcessor = blockStateChangeProcessor;
            _blockShaderAnimation = gameObject.AddComponent<BlockShaderAnimation>();

            foreach (var child in gameObject.GetComponentsInChildren<BlockGameObjectChild>()) child.Init(this);

            _rendererMaterialReplacer = new List<RendererMaterialReplacer>();
            foreach (var renderer in GetComponentsInChildren<Renderer>()) _rendererMaterialReplacer.Add(new RendererMaterialReplacer(renderer));
        }

        public async UniTask PlayPlaceAnimation()
        {
            _isShaderAnimationing = true;
            await _blockShaderAnimation.PlaceAnimation();
            _isShaderAnimationing = false;
        }

        public void SetRemovePreviewMaterial()
        {
            if (_isShaderAnimationing) return;
            var placePreviewMaterial = Resources.Load<Material>(MaterialConst.PreviewRemoveBlockMaterial);
            foreach (var replacer in _rendererMaterialReplacer) replacer.SetMaterial(placePreviewMaterial);
        }

        public void ResetMaterial()
        {
            if (_isShaderAnimationing) return;
            foreach (var replacer in _rendererMaterialReplacer) replacer.ResetMaterial();
        }

        public async UniTask DestroyBlock()
        {
            _isShaderAnimationing = true;
            await _blockShaderAnimation.RemoveAnimation();
            Destroy(gameObject);
        }
    }
}