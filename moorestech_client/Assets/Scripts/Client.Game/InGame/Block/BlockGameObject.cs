using System.Collections.Generic;
using System.Linq;
using Client.Common;
using Client.Game.InGame.BlockSystem.StateProcessor;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using Mooresmaster.Model.BlocksModule;
using UnityEngine;
using UnityEngine.VFX;

namespace Client.Game.InGame.Block
{
    public class BlockGameObject : MonoBehaviour
    {
        public BlockId BlockId { get; private set; }
        public BlockMasterElement BlockMasterElement { get; private set; }
        public BlockPositionInfo BlockPosInfo { get; private set; }
        public List<IBlockStateChangeProcessor> BlockStateChangeProcessors { get; private set; }
        
        private BlockShaderAnimation _blockShaderAnimation;
        private RendererMaterialReplacerController _rendererMaterialReplacerController;
        private bool _isShaderAnimating;
        private List<VisualEffect> _visualEffects = new();
        
        
        public void Initialize(BlockMasterElement blockMasterElement, BlockPositionInfo posInfo)
        {
            BlockPosInfo = posInfo;
            BlockId = MasterHolder.BlockMaster.GetBlockId(blockMasterElement.BlockGuid);
            BlockMasterElement = blockMasterElement;
            BlockStateChangeProcessors = gameObject.GetComponentsInChildren<IBlockStateChangeProcessor>().ToList();
            _visualEffects = gameObject.GetComponentsInChildren<VisualEffect>(true).ToList();
            _blockShaderAnimation = gameObject.AddComponent<BlockShaderAnimation>();
           
            _rendererMaterialReplacerController = new RendererMaterialReplacerController(gameObject);
            
            // 子供のBlockGameObjectChildを初期化
            foreach (var child in gameObject.GetComponentsInChildren<BlockGameObjectChild>()) child.Init(this);
            
            // 地面との衝突判定を無効化
            foreach (var groundCollisionDetector in gameObject.GetComponentsInChildren<GroundCollisionDetector>(true))
            {
                groundCollisionDetector.enabled = false;
            }
        }
        
        public async UniTask PlayPlaceAnimation()
        {
            _isShaderAnimating = true;
            SetVfxActive(false);
            await _blockShaderAnimation.PlaceAnimation();
            _isShaderAnimating = false;
            SetVfxActive(true);
        }
        
        public void SetRemovePreviewing()
        {
            if (_isShaderAnimating) return;
            var placePreviewMaterial = Resources.Load<Material>(MaterialConst.PreviewPlaceBlockMaterial);
            
            _rendererMaterialReplacerController.CopyAndSetMaterial(placePreviewMaterial);
            _rendererMaterialReplacerController.SetColor(MaterialConst.PreviewColorPropertyName ,MaterialConst.NotPlaceableColor);
            Resources.UnloadAsset(placePreviewMaterial);
        }
        
        public void ResetMaterial()
        {
            if (_isShaderAnimating) return;
            _rendererMaterialReplacerController.ResetMaterial();
        }
        
        public async UniTask DestroyBlock()
        {
            _isShaderAnimating = true;
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