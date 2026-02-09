using Client.Common.Server;
using Client.Game.InGame.Entity.Object.Util;
using Core.Master;
using System.Collections.Generic;
using UnityEngine;

namespace Client.Game.InGame.Entity.Object
{
    public class BeltConveyorItemEntityObject : MonoBehaviour, IEntityObject
    {
        public long EntityId { get; private set; }

        [SerializeField] private MeshRenderer meshRenderer;
        [SerializeField] private Material itemMaterial;

        private static readonly Dictionary<ItemId, Material> MaterialCache = new();

        private float _linerTime;
        private Vector3 _previousPosition;
        
        private Vector3 _targetPosition;
        
        private void Awake()
        {
            _targetPosition = transform.position;
            _previousPosition = transform.position;
        }
        
        public void Initialize(long entityId)
        {
            EntityId = entityId;
        }
        
        //Linerでポジションを補完させる
        private void Update()
        {
            //補完する
            var rate = _linerTime / NetworkConst.UpdateIntervalSeconds;
            rate = Mathf.Clamp01(rate);
            transform.position = Vector3.Lerp(_previousPosition, _targetPosition, rate);
            _linerTime += Time.deltaTime;
        }
        
        public void SetDirectPosition(Vector3 position)
        {
            _targetPosition = position;
            _previousPosition = position;
            transform.position = position;
        }
        
        public void SetPositionWithLerp(Vector3 position)
        {
            _previousPosition = transform.position;
            _targetPosition = position;
            _linerTime = 0;
        }
        public void SetEntityData(byte[] entityEntityData)
        {
            var pos = BeltConveyorItemPositionCalculator.CalculatePosition(entityEntityData);
            SetPositionWithLerp(pos);
        }
        
        public void Destroy()
        {
            Destroy(gameObject);
        }
        
        public void SetTexture(Texture texture, ItemId itemId)
        {
            // キャッシュから取得
            // Retrieve from material cache
            if (MaterialCache.TryGetValue(itemId, out var cachedMaterial))
            {
                meshRenderer.material = cachedMaterial;
                return;
            }

            // キャッシュにない場合は新規作成
            // Create new material if not cached
            var newMaterial = new Material(itemMaterial) { mainTexture = texture };
            if (texture != null)
            {
                MaterialCache[itemId] = newMaterial;
            }
            meshRenderer.material = newMaterial;
        }
    }
}
