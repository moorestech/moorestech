using Client.Game.InGame.Context;
using Client.Game.InGame.Entity.Object;
using Client.Network.API;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Entity.Interface;
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
            
            // State文字列からItemIdを取得してテクスチャを設定
            // Get ItemId from State string and set texture
            var id = new ItemId(int.Parse(entity.State.Split(',')[0]));
            var viewData = ClientContext.ItemImageContainer.GetItemView(id);
            Texture texture = null;
            if (viewData != null)
            {
                texture = viewData.ItemTexture;
            }
            
            var item = itemObject.GetComponent<ItemEntityObject>();
            item.SetTexture(texture);
            return item;
        }
    }
}

