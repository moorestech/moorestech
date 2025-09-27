using System.Linq;
using Client.Common;
using Client.Game.InGame.Block;
using Client.Game.InGame.Context;
using Core.Master;
using Mooresmaster.Model.BlocksModule;
using UnityEngine;
using UnityEngine.VFX;

namespace Client.Game.InGame.BlockSystem.PlaceSystem
{
    public class BlockPreviewObject : MonoBehaviour
    {
        public BlockMasterElement BlockMasterElement { get; private set; }
        
        public bool IsCollisionGround
        {
            get
            {
                foreach (var collisionDetector in _collisionDetectors)
                {
                    if (collisionDetector.IsCollision) return true;
                }
                return false;
            }
        }
        private GroundCollisionDetector[] _collisionDetectors;
        private RendererMaterialReplacerController _rendererMaterialReplacerController;
        
        public void Initialize(BlockId blockId)
        {
            BlockMasterElement = MasterHolder.BlockMaster.GetBlockMaster(blockId);
            
            var placeMaterial = Resources.Load<Material>(MaterialConst.PreviewPlaceBlockMaterial);
            _rendererMaterialReplacerController = new RendererMaterialReplacerController(gameObject);
            _rendererMaterialReplacerController.CopyAndSetMaterial(placeMaterial);
            
            _collisionDetectors = GetComponentsInChildren<GroundCollisionDetector>(true);
            
            SetPlaceableColor(true);
            
            var visualEffects = GetComponentsInChildren<VisualEffect>(false);
            foreach (var visualEffect in visualEffects) visualEffect.gameObject.SetActive(false);
            
            // プレビュー限定オブジェクトをオンに
            // Turn on preview-only object
            var previewOnlyObjects = gameObject.GetComponentsInChildren<IPreviewOnlyObject>(true).ToList();
            previewOnlyObjects.ForEach(obj =>
            {
                obj.Initialize(blockId);
                obj.SetActive(true);
            });
        }
        
        public void SetPlaceableColor(bool isPlaceable)
        {
            var color = isPlaceable ? MaterialConst.PlaceableColor : MaterialConst.NotPlaceableColor;
            _rendererMaterialReplacerController.SetColor(MaterialConst.PreviewColorPropertyName, color);
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