using System;
using System.Linq;
using Client.Common;
using Client.Game.InGame.Block;
using Client.Game.InGame.Block.Preview;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewObject;
using Core.Master;
using Mooresmaster.Model.BlocksModule;
using Server.Protocol.PacketResponse;
using UnityEngine;
using UnityEngine.VFX;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController
{
    public class BlockPreviewObject : MonoBehaviour
    {
        public BlockMasterElement BlockMasterElement { get; private set; }
        
        private RendererMaterialReplacerController _rendererMaterialReplacerController;
        private IBlockPreviewStateProcessor[] _blockPreviewStateProcessors;
        
        public void Initialize(BlockId blockId)
        {
            BlockMasterElement = MasterHolder.BlockMaster.GetBlockMaster(blockId);
            
            var placeMaterial = MaterialConst.GetPreviewPlaceBlockMaterial();
            _rendererMaterialReplacerController = new RendererMaterialReplacerController(gameObject);
            _rendererMaterialReplacerController.CopyAndSetMaterial(placeMaterial);
            
            _blockPreviewStateProcessors = GetComponentsInChildren<IBlockPreviewStateProcessor>(true);
            
            SetPlaceableColor(true);
            
            var visualEffects = GetComponentsInChildren<VisualEffect>(false);
            foreach (var visualEffect in visualEffects) visualEffect.gameObject.SetActive(false);
            
            // プレビュー限定オブジェクトをオンに
            // Turn on preview-only object
            BlockPreviewOnlyObjects.Initialize(gameObject, blockId, true);
        }
        
        public void SetPreviewStateDetail(PlaceInfo placeInfo)
        {
            foreach (var processor in _blockPreviewStateProcessors)
            {
                try
                {
                    processor.SetPreviewStateDetail(placeInfo);
                }
                catch (Exception e)
                {
                    Debug.LogError($"BlockPreviewObject SetPreviewStateDetail Error: {e.Message} \n {e.StackTrace}");
                }
            }
        }
        
        public void SetPlaceableColor(bool isPlaceable)
        {
            var color = isPlaceable ? MaterialConst.PlaceableColor : MaterialConst.NotPlaceableColor;
            _rendererMaterialReplacerController.SetColor(MaterialConst.PreviewColorPropertyName, color);
        }
        
        public void SetActive(bool active)
        {
            gameObject.SetActive(active);
        }
        
        public void SetTransform(Vector3 pos, Quaternion rotation)
        {
            transform.position = pos;
            transform.rotation = rotation;
        }
        
        public void Destroy()
        {
            _rendererMaterialReplacerController.DestroyMaterial();
            Destroy(gameObject);
        }
    }
}
