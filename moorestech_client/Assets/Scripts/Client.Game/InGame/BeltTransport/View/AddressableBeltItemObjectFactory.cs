using System.Collections.Generic;
using Client.Common.Asset;
using Client.Game.InGame.Context;
using CommandForgeGenerator.Command;
using Core.Master;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Game.InGame.BeltTransport.View
{
    // 種類にモデルがあればそのprefab、無ければテクスチャを貼る既定のアイテムprefabを置く。prefabは種類ごとに1回だけ読む
    // Places the kind's model prefab when it has one, otherwise the default item prefab with the kind's texture; each prefab is loaded once per kind
    // 置いた表示物は1つの親の下に集め、スキットのエンティティ窓口はその親ごと表示を切り替える
    // Placed objects are gathered under one parent, and the skit's entity window toggles that parent as a whole
    public sealed class AddressableBeltItemObjectFactory : IBeltItemObjectFactory, ISkitEntityObjectControl
    {
        private const string DefaultItemPrefabPath = "Vanilla/Game/ItemEntity";
        private const string ParentName = "BeltTransportItems";

        private GameObject _defaultItemPrefab;
        private readonly Dictionary<ItemId, GameObject> _modelPrefabByItem = new();
        private Transform _parent;

        public async UniTask<IBeltItemObject> CreateAsync(ItemId itemId, Vector3 position)
        {
            var itemMaster = MasterHolder.ItemMaster.GetItemMaster(itemId);
            var modelPath = itemMaster.AddressablePaths?.EntityModel;
            if (string.IsNullOrEmpty(modelPath)) return await CreateTextured(itemId, position);

            var modelPrefab = await LoadModelPrefab(itemId, modelPath);
            if (modelPrefab == null) return await CreateTextured(itemId, position);
            return new GameObjectBeltItemObject(Object.Instantiate(modelPrefab, position, Quaternion.identity, Parent()));
        }

        private async UniTask<IBeltItemObject> CreateTextured(ItemId itemId, Vector3 position)
        {
            if (_defaultItemPrefab == null) _defaultItemPrefab = await AddressableLoader.LoadAsyncDefault<GameObject>(DefaultItemPrefabPath);
            var itemObject = Object.Instantiate(_defaultItemPrefab, position, Quaternion.identity, Parent());
            var viewData = ClientContext.ItemImageContainer.GetItemView(itemId);
            itemObject.GetComponent<BeltItemTexturedObject>().SetTexture(viewData?.ItemTexture, itemId);
            return new GameObjectBeltItemObject(itemObject);
        }

        private async UniTask<GameObject> LoadModelPrefab(ItemId itemId, string modelPath)
        {
            if (_modelPrefabByItem.TryGetValue(itemId, out var cached)) return cached;
            // Addressableの読み込み結果は外部データ。失敗はログに残し、既定のprefabへ落とす
            // The Addressable load result is external data; a failure is logged and falls back to the default prefab
            var loaded = await AddressableLoader.LoadAsync<GameObject>(modelPath);
            if (loaded?.Asset == null)
            {
                Debug.LogError($"[BeltTransportView] failed to load the item model {modelPath} for item {itemId.AsPrimitive()}; showing the textured default instead.");
                return null;
            }
            _modelPrefabByItem[itemId] = loaded.Asset;
            return loaded.Asset;
        }

        public void SetActive(bool enable)
        {
            Parent().gameObject.SetActive(enable);
        }

        private Transform Parent()
        {
            if (_parent == null) _parent = new GameObject(ParentName).transform;
            return _parent;
        }
    }
}
