using Client.Common.Asset;
using Client.Game.InGame.Context;
using Client.Game.InGame.Entity;
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
    /// ベルトコンベア上のアイテムエンティティを生成するファクトリー
    /// Factory to create belt conveyor item entity
    /// </summary>
    public class BeltConveyorItemEntityObjectFactory : IEntityObjectFactory
    {
        private const string DefaultItemPrefabPath = "Vanilla/Game/ItemEntity";

        private GameObject _defaultItemPrefab;

        /// <summary>
        /// ベルトコンベア上のアイテムエンティティを生成
        /// Create belt conveyor item entity
        /// </summary>
        public async UniTask<IEntityObject> CreateEntity(Transform parent, EntityResponse entity)
        {
            // StateデータからItemIdを復元
            // Restore ItemId from state data
            var itemState = DeserializeState();
            var itemId = new ItemId(itemState.ItemId);
            var itemMaster = MasterHolder.ItemMaster.GetItemMaster(itemId);
            var hasEntityData = entity.EntityData != null && entity.EntityData.Length > 0;

            // カスタムモデルパスが設定されている場合
            // If custom model path is set
            if (!string.IsNullOrEmpty(itemMaster.AddressablePaths?.EntityModel))
            {
                return await CreateCustomModelEntity(parent, entity, itemMaster.AddressablePaths.EntityModel, itemId);
            }

            return await CreateTextureBasedEntity(parent, entity, itemId);

            #region Internal

            BeltConveyorItemEntityStateMessagePack DeserializeState()
            {
                // データが空の場合は既定値を返す
                // Return default state when data is empty
                if (entity.EntityData == null || entity.EntityData.Length == 0) return new BeltConveyorItemEntityStateMessagePack();
                return MessagePackSerializer.Deserialize<BeltConveyorItemEntityStateMessagePack>(entity.EntityData);
            }

            async UniTask<IEntityObject> CreateTextureBasedEntity(Transform parentTransform, EntityResponse entityResponse, ItemId id)
            {
                // デフォルトPrefabをロード（初回のみ）
                // Load default prefab (only first time)
                if (_defaultItemPrefab == null)
                {
                    _defaultItemPrefab = await AddressableLoader.LoadAsyncDefault<GameObject>(DefaultItemPrefabPath);
                }

                // テクスチャベースのエンティティを生成
                // Create texture-based entity
                var itemObject = GameObject.Instantiate(_defaultItemPrefab, entityResponse.Position, Quaternion.identity, parentTransform);

                var viewData = ClientContext.ItemImageContainer.GetItemView(id);
                Texture texture = null;
                if (viewData != null)
                {
                    texture = viewData.ItemTexture;
                }

                var item = itemObject.GetComponent<BeltConveyorItemEntityObject>();
                item.Initialize(entityResponse.InstanceId);
                item.SetTexture(texture);
                return item;
            }

            async UniTask<IEntityObject> CreateCustomModelEntity(Transform parentTransform, EntityResponse entityResponse, string addressablePath, ItemId id)
            {
                // カスタムモデルをロード（LoadAsyncでLoadedAssetを取得）
                // Load custom model (get LoadedAsset with LoadAsync)
                using var loadedAsset = await AddressableLoader.LoadAsync<GameObject>(addressablePath);

                // ロード失敗時はテクスチャベースにフォールバック
                // Fallback to texture-based if load fails
                if (loadedAsset?.Asset == null)
                {
                    Debug.LogError($"Failed to load custom entity model: {addressablePath}. Falling back to texture-based display.");
                    return await CreateTextureBasedEntity(parentTransform, entityResponse, id);
                }

                // カスタムモデルをインスタンス化
                // Instantiate custom model
                var customModelObject = GameObject.Instantiate(loadedAsset.Asset, entityResponse.Position, Quaternion.identity, parentTransform);

                // CustomModelBeltConveyorItemEntityObjectコンポーネントを追加
                // Add CustomModelBeltConveyorItemEntityObject component
                var customModelEntity = customModelObject.AddComponent<CustomModelBeltConveyorItemEntityObject>();
                customModelEntity.Initialize(entityResponse.InstanceId);
                return customModelEntity;
            }

            #endregion
        }
    }
}
