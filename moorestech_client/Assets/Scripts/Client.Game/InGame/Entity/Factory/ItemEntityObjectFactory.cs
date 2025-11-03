using Client.Game.InGame.Context;
using Client.Game.InGame.Entity.Object;
using Client.Network.API;
using Core.Master;
using Game.Entity.Interface;
using UnityEngine;

namespace Client.Game.InGame.Entity.Factory
{
    /// <summary>
    /// アイテムエンティティを生成するファクトリー
    /// Factory to create item entity
    /// </summary>
    public class ItemEntityObjectFactory : MonoBehaviour, IEntityObjectFactory
    {
        [SerializeField] private ItemEntityObject itemPrefab;
        
        public string SupportedEntityType => VanillaEntityType.VanillaItem;
        
        /// <summary>
        /// アイテムエンティティを生成
        /// Create item entity
        /// </summary>
        public IEntityObject CreateEntity(EntityResponse entity)
        {
            var item = Instantiate(itemPrefab, entity.Position, Quaternion.identity, transform);
            
            // State文字列からItemIdを取得してテクスチャを設定
            // Get ItemId from State string and set texture
            var id = new ItemId(int.Parse(entity.State.Split(',')[0]));
            var viewData = ClientContext.ItemImageContainer.GetItemView(id);
            Texture texture = null;
            if (viewData != null)
            {
                texture = viewData.ItemTexture;
            }
            
            item.SetTexture(texture);
            return item;
        }
    }
}

