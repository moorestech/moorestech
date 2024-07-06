using System.Collections.Generic;
using System.Linq;
using Client.Common;
using Client.Game.InGame.BlockSystem.StateProcessor;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using Game.Block.Interface.BlockConfig;
using UnityEngine;
using UnityEngine.VFX;

namespace Client.Game.InGame.Block
{
    public class BlockGameObject : MonoBehaviour
    {
        public int BlockId { get; private set; }
        public BlockConfigData BlockConfig { get; private set; }
        public BlockPositionInfo BlockPosInfo { get; private set; }
        public IBlockStateChangeProcessor BlockStateChangeProcessor { get; private set; }
        
        private BlockShaderAnimation _blockShaderAnimation;
        private RendererMaterialReplacerController _rendererMaterialReplacerController;
        private bool _isShaderAnimationing;
        private List<VisualEffect> _visualEffects;
        
        
        public void Initialize(BlockConfigData blockConfig, BlockPositionInfo posInfo, IBlockStateChangeProcessor blockStateChangeProcessor)
        {
            BlockPosInfo = posInfo;
            BlockId = blockConfig.BlockId;
            BlockConfig = blockConfig;
            BlockStateChangeProcessor = blockStateChangeProcessor;
            _visualEffects = gameObject.GetComponentsInChildren<VisualEffect>(true).ToList();
            _blockShaderAnimation = gameObject.AddComponent<BlockShaderAnimation>();
            
            foreach (var child in gameObject.GetComponentsInChildren<BlockGameObjectChild>()) child.Init(this);
            
            _rendererMaterialReplacerController = new RendererMaterialReplacerController(gameObject);
        }
        
        public async UniTask PlayPlaceAnimation()
        {
            _isShaderAnimationing = true;
            SetVfxActive(false);
            await _blockShaderAnimation.PlaceAnimation();
            _isShaderAnimationing = false;
            SetVfxActive(true);
        }
        
        public void SetRemovePreviewing()
        {
            if (_isShaderAnimationing) return;
            _isShaderAnimationing = true;
            var placePreviewMaterial = Resources.Load<Material>(MaterialConst.PreviewPlaceBlockMaterial);
            
            _rendererMaterialReplacerController.CopyAndSetMaterial(placePreviewMaterial);
            _rendererMaterialReplacerController.SetColor(MaterialConst.NotPlaceableColor);
            Resources.UnloadAsset(placePreviewMaterial);
        }
        
        public void ResetMaterial()
        {
            if (_isShaderAnimationing) return;
            _rendererMaterialReplacerController.ResetMaterial();
        }
        
        public async UniTask DestroyBlock()
        {
            _isShaderAnimationing = true;
            SetVfxActive(false);
            await _blockShaderAnimation.RemoveAnimation();
            Destroy(gameObject);
        }
        
        private void SetVfxActive(bool isActive)
        {
            foreach (var vfx in _visualEffects) vfx.gameObject.SetActive(isActive);
        }
    }
}