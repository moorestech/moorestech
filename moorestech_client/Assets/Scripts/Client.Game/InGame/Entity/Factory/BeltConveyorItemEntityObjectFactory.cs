using System.Collections.Generic;
using Client.Common.Asset;
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
    public sealed class BeltConveyorItemEntityObjectFactory : IEntityObjectFactory
    {
        private const string DefaultItemPrefabPath = "Vanilla/Game/ItemEntity";
        private readonly Dictionary<ItemId, GameObject> _customModelPrefabs = new();
        private GameObject _defaultItemPrefab;
        public async UniTask<IEntityObject> CreateEntity(Transform parent, EntityResponse entity)
        {
            var state = MessagePackSerializer.Deserialize<BeltConveyorItemEntityStateMessagePack>(entity.EntityData);
            return await CreateItem(parent, entity.InstanceId, new ItemId(state.ItemId), entity.Position);
        }
        public async UniTask<IEntityObject> CreateItem(Transform parent, long identity, ItemId itemId, Vector3 position)
        {
            // 既存のモデル指定とテクスチャ表示を共通の生成処理へ渡す。
            // Use the existing custom-model and texture rendering paths for item creation.
            var master = MasterHolder.ItemMaster.GetItemMaster(itemId);
            var path = master.AddressablePaths?.EntityModel;
            if (!string.IsNullOrEmpty(path))
            {
                if (!_customModelPrefabs.TryGetValue(itemId, out var prefab))
                {
                    var loaded = await AddressableLoader.LoadAsync<GameObject>(path);
                    if (loaded?.Asset == null)
                    {
                        Debug.LogError($"Failed to load custom entity model: {path}. Falling back to texture-based display.");
                        return await CreateTextureItem();
                    }
                    prefab = loaded.Asset;
                    _customModelPrefabs[itemId] = prefab;
                }
                var instance = UnityEngine.Object.Instantiate(prefab, position, Quaternion.identity, parent);
                var view = instance.AddComponent<CustomModelBeltConveyorItemEntityObject>();
                view.Initialize(identity);
                view.SetDirectPosition(position);
                return view;
            }
            return await CreateTextureItem();

            #region Internal
            async UniTask<IEntityObject> CreateTextureItem()
            {
                // シーン既存の親へ生成し、表示状態も引き継ぐ。
                // Instantiate under the existing scene parent and inherit its visibility.
                if (_defaultItemPrefab == null)
                    _defaultItemPrefab = await AddressableLoader.LoadAsyncDefault<GameObject>(DefaultItemPrefabPath);
                var instance = UnityEngine.Object.Instantiate(_defaultItemPrefab, position, Quaternion.identity, parent);
                var view = instance.GetComponent<BeltConveyorItemEntityObject>();
                view.Initialize(identity);
                view.SetTexture(ClientContext.ItemImageContainer.GetItemView(itemId)?.ItemTexture, itemId);
                view.SetDirectPosition(position);
                return view;
            }
            #endregion
        }
    }
}
