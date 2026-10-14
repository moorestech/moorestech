using System;
using System.Collections.Generic;
using System.Linq;
using Client.Common;
using Client.Game.InGame.Block;
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
        private Collider[][] _contactColliders;
        private bool _terrainContactConfigured;
        private GameObject _ownedTerrainContact;
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
            var previewOnlyObjects = gameObject.GetComponentsInChildren<IPreviewOnlyObject>(true).ToList();
            previewOnlyObjects.ForEach(obj =>
            {
                obj.Initialize(blockId);
                obj.SetActive(true);
            });
            CacheContactColliders();
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
        
        internal void ConfigurePlacementColliders()
        {
            // 地面接触を残し、非表示の子も設置照準から外す
            // Preserve ground contact and exclude inactive children from placement aiming.
            foreach (var collider in GetComponentsInChildren<Collider>(true))
            {
                collider.isTrigger = true;
                collider.gameObject.layer = LayerConst.BlockLayer;
            }
        }

        internal void ConfigureTerrainContact(Vector3Int blockSize)
        {
            if (_terrainContactConfigured) return;
            _terrainContactConfigured = true;
            CacheContactColliders();
            if (_collisionDetectors.Length != 0) return;

            // 見た目の張り出しを避け、マスターの占有セル内だけで地形接触を測る
            // Measure terrain within master occupancy, excluding visual mesh overhang.
            var contact = new GameObject("PlacementGroundContactVolume");
            _ownedTerrainContact = contact;
            contact.layer = LayerConst.BlockLayer;
            contact.transform.SetParent(transform, false);
            var body = contact.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            var size = new Vector3(blockSize.x, blockSize.y, blockSize.z);
            var inset = Physics.defaultContactOffset * 2f;
            var box = contact.AddComponent<BoxCollider>();
            box.center = size * 0.5f;
            box.size = size - Vector3.one * (inset * 2f);
            box.isTrigger = true;
            contact.AddComponent<GroundCollisionDetector>();
            CacheContactColliders();
        }

        internal void SetTerrainContactEnabled(bool enabled, Vector3Int blockSize)
        {
            // 通常設置は専用源を作らず、再利用時には専用源だけを切り替える
            // Normal placement creates no owned source; reuse toggles only that source.
            if (enabled) ConfigureTerrainContact(blockSize);
            if (_ownedTerrainContact != null) _ownedTerrainContact.SetActive(enabled);
        }

        internal bool DetectCurrentGroundOverlap()
        {
            return PlacementGroundContactQuery.HasOverlap(_collisionDetectors, _contactColliders, gameObject.scene);
        }

        private void CacheContactColliders()
        {
            // 非表示の子も構成時に保持し、現在状態だけを問い合わせで読む
            // Cache dormant child sources once and read only their live state during queries.
            _collisionDetectors = GetComponentsInChildren<GroundCollisionDetector>(true);
            _contactColliders = new Collider[_collisionDetectors.Length][];
            for (var index = 0; index < _collisionDetectors.Length; index++)
                _contactColliders[index] = _collisionDetectors[index].GetComponentsInChildren<Collider>(true);
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
