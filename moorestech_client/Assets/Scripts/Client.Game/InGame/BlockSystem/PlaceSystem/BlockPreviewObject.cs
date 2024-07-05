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
        
        private Material _placeMaterial;
        
        public void Initialize(BlockConfigData blockConfigData)
        {
            BlockConfig = blockConfigData;
            
            _placeMaterial = Resources.Load<Material>(MaterialConst.PreviewPlaceBlockMaterial);
            var blockRenderer = GetComponentsInChildren<Renderer>();
            foreach (var renderer in blockRenderer)
            {
                var replacer = new RendererMaterialReplacer(renderer);
                replacer.CopyAndSetMaterial(_placeMaterial);
            }
            
            SetVfxActive(false);
        }
        
        public void SetPlaceableColor(bool isPlaceable)
        {
            var color = isPlaceable ? MaterialConst.PlaceableColor : MaterialConst.NotPlaceableColor;
            _placeMaterial.color = color;
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
            Destroy(_placeMaterial);
            Destroy(gameObject);
        }
    }
}