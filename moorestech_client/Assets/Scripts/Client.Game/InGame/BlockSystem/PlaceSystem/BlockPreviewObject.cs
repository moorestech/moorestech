using Client.Common;
using Client.Game.InGame.Block;
using Game.Block.Interface.BlockConfig;
using UnityEngine;
using UnityEngine.VFX;

namespace Client.Game.InGame.BlockSystem.PlaceSystem
{
    public class BlockPreviewObject : MonoBehaviour
    {
        public BlockConfigData BlockConfig { get; private set; }
        
        private RendererMaterialReplacerController _rendererMaterialReplacerController;
        
        public void Initialize(BlockConfigData blockConfigData)
        {
            BlockConfig = blockConfigData;
            
            _rendererMaterialReplacerController = new RendererMaterialReplacerController(gameObject);
            
            var placeMaterial = Resources.Load<Material>(MaterialConst.PreviewPlaceBlockMaterial);
            _rendererMaterialReplacerController.CopyAndSetMaterial(placeMaterial);
            
            SetVfxActive(false);
            SetPlaceableColor(true);
        }
        
        public void SetPlaceableColor(bool isPlaceable)
        {
            var color = isPlaceable ? MaterialConst.PlaceableColor : MaterialConst.NotPlaceableColor;
            _rendererMaterialReplacerController.SetColor(color);
        }
        
        private void SetVfxActive(bool isActive)
        {
            var visualEffects = GetComponentsInChildren<VisualEffect>(isActive);
            foreach (var visualEffect in visualEffects) visualEffect.gameObject.SetActive(false);
        }
        
        public void SetTriggerCollider(bool isTrigger)
        {
            var childrenColliders = GetComponentsInChildren<Collider>();
            foreach (var childrenCollider in childrenColliders) childrenCollider.isTrigger = isTrigger;
        }
        
        public void SetActive(bool active)
        {
            gameObject.SetActive(active);
        }
        
        public void Destroy()
        {
            _rendererMaterialReplacerController.DestroyMaterial();
            Destroy(gameObject);
        }
    }
}