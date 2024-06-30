using Client.Game.InGame.Block;
using Game.Block.Interface.BlockConfig;
using UnityEngine;
using UnityEngine.VFX;

namespace Client.Game.InGame.BlockSystem.PlaceSystem
{
    public class BlockPreviewObject : MonoBehaviour
    {
        public BlockConfigData BlockConfig { get; private set; }
        
        public void Initialize(BlockConfigData blockConfigData, Material material)
        {
            BlockConfig = blockConfigData;
            
            SetMaterial(material);
            SetVfxActive(false);
        }
        
        public void SetMaterial(Material material)
        {
            var blockRenderer = GetComponentsInChildren<Renderer>();
            foreach (var renderer in blockRenderer)
            {
                var replacer = new RendererMaterialReplacer(renderer);
                replacer.SetMaterial(material);
            }
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
    }
}