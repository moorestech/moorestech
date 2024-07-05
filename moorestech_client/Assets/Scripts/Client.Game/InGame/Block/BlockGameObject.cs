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
        private BlockShaderAnimation _blockShaderAnimation;
        private bool _isShaderAnimationing;
        private List<RendererMaterialReplacer> _rendererMaterialReplacer;
        private List<VisualEffect> _visualEffects;
        public int BlockId { get; private set; }
        public BlockConfigData BlockConfig { get; private set; }
        public BlockPositionInfo BlockPosInfo { get; private set; }
        public IBlockStateChangeProcessor BlockStateChangeProcessor { get; private set; }
        
        public void Initialize(BlockConfigData blockConfig, BlockPositionInfo posInfo, IBlockStateChangeProcessor blockStateChangeProcessor)
        {
            BlockPosInfo = posInfo;
            BlockId = blockConfig.BlockId;
            BlockConfig = blockConfig;
            BlockStateChangeProcessor = blockStateChangeProcessor;
            _visualEffects = gameObject.GetComponentsInChildren<VisualEffect>(true).ToList();
            _blockShaderAnimation = gameObject.AddComponent<BlockShaderAnimation>();
            
            foreach (var child in gameObject.GetComponentsInChildren<BlockGameObjectChild>()) child.Init(this);
            
            _rendererMaterialReplacer = new List<RendererMaterialReplacer>();
            foreach (var renderer in GetComponentsInChildren<Renderer>()) _rendererMaterialReplacer.Add(new RendererMaterialReplacer(renderer));
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
            var placePreviewMaterial = Resources.Load<Material>(MaterialConst.PreviewPlaceBlockMaterial);
            placePreviewMaterial.color = MaterialConst.NotPlaceableColor;
            foreach (var replacer in _rendererMaterialReplacer) replacer.CopyAndSetMaterial(placePreviewMaterial);
        }
        
        public void ResetMaterial()
        {
            if (_isShaderAnimationing) return;
            foreach (var replacer in _rendererMaterialReplacer) replacer.ResetMaterial();
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