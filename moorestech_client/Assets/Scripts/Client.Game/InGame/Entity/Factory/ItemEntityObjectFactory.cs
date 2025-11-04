using Client.Game.InGame.Context;
using Client.Game.InGame.Entity.Object;
using Client.Network.API;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Entity.Interface;
using MessagePack;
using UnityEngine;

namespace Client.Game.InGame.Entity.Factory
{
    /// <summary>
    /// アイテムエンティティを生成するファクトリー
    /// Factory to create item entity
    /// </summary>
    public class ItemEntityObjectFactory : IEntityObjectFactory
    {
        private const string AddressablePath = "Vanilla/Game/ItemEntity";
        
        private readonly GameObject _itemPrefab;
        
        public ItemEntityObjectFactory()
        {
            _itemPrefab = Resources.Load<GameObject>(AddressablePath);
        }
        
        /// <summary>
        /// アイテムエンティティを生成
        /// Create item entity
        /// </summary>
        public async UniTask<IEntityObject> CreateEntity(Transform parent, EntityResponse entity)
        {
            var itemObject = GameObject.Instantiate(_itemPrefab, entity.Position, Quaternion.identity, parent);
            
            // StateデータからItemIdを復元してテクスチャを設定
            // Restore ItemId from state data and set texture
            var itemState = DeserializeState();
            var id = new ItemId(itemState.ItemId);
            var viewData = ClientContext.ItemImageContainer.GetItemView(id);
            Texture texture = null;
            if (viewData != null)
            {
                texture = viewData.ItemTexture;
            }
            
            var item = itemObject.GetComponent<ItemEntityObject>();
            item.SetTexture(texture);
            return item;
            
            #region Internal
            
            ItemEntityStateMessagePack DeserializeState()
            {
                // データが空の場合は既定値を返す
                // Return default state when data is empty
                if (entity.EntityData == null || entity.EntityData.Length == 0) return new ItemEntityStateMessagePack();
                return MessagePackSerializer.Deserialize<ItemEntityStateMessagePack>(entity.EntityData);
            }
            
            #endregion
        }
    }
}
