using System;
using System.Collections.Generic;
using Client.Network.NewApi;
using Game.Entity.Interface;
using Constant;
using MainGame.UnityView.Entity;
using MainGame.UnityView.Item;
using MainGame.UnityView.UI.Inventory.Element;
using UnityEngine;
using VContainer;

namespace MainGame.Presenter.Entity
{
    public class EntityObjectDatastore : MonoBehaviour
    {
        [SerializeField] private ItemEntityObject itemPrefab;

        private readonly Dictionary<long, (DateTime lastUpdate, IEntityObject objectEntity)> _entities = new();

        private ItemImageContainer _itemImageContainer;


        /// <summary>
        ///     エンティティ最終更新時間をチェックし、一定時間経過していたら削除する
        /// </summary>
        private void Update()
        {
            //0.2秒以上経過していたら削除
            var removeEntities = new List<long>();
            foreach (var entity in _entities)
                if ((DateTime.Now - entity.Value.lastUpdate).TotalSeconds > 0.2)
                    removeEntities.Add(entity.Key);
            foreach (var removeEntity in removeEntities)
            {
                _entities[removeEntity].objectEntity.Destroy();
                _entities.Remove(removeEntity);
            }
        }

        [Inject]
        public void Construct(ItemImageContainer itemImageContainer)
        {
            _itemImageContainer = itemImageContainer;
        }

        /// <summary>
        /// エンティティの生成、更新を行う
        /// </summary>
        public void OnEntitiesUpdate(List<EntityResponse> entities)
        {
            foreach (var entity in entities)
            {
                if (_entities.ContainsKey(entity.InstanceId))
                {
                    _entities[entity.InstanceId].objectEntity.SetInterpolationPosition(entity.Position);
                    _entities[entity.InstanceId] = (DateTime.Now, _entities[entity.InstanceId].objectEntity);
                }
                else
                {
                    var entityObject = CreateEntity(entity);
                    _entities.Add(entity.InstanceId, (DateTime.Now, entityObject));
                }
            }
        }

        /// <summary>
        ///     タイプに応じたエンティティの作成
        /// </summary>
        private IEntityObject CreateEntity(EntityResponse entity)
        {
            if (entity.Type == VanillaEntityType.VanillaItem)
            {
                var item = Instantiate(itemPrefab, entity.Position, Quaternion.identity, transform);


                var id = int.Parse(entity.State.Split(',')[0]);
                item.SetTexture(_itemImageContainer.GetItemView(id).ItemTexture);
                return item;
            }

            throw new ArgumentException("エンティティタイプがありません");
        }
    }
}