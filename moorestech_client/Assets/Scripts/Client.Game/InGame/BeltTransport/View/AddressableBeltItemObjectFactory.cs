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
        private const string MissingItemObjectName = "MissingBeltItem";

        // 結果でなく進行中の読み込みを持つ。同じ種類が同時に何個現れても読み込みは1本で、失敗(null)もそのまま印として残る
        // Holds in-flight loads rather than results; however many of a kind appear at once there is one load, and a failure (null) stays as the mark
        private UniTask<GameObject>? _defaultItemPrefabTask;
        private readonly Dictionary<ItemId, UniTask<GameObject>> _modelPrefabTaskByItem = new();
        private Transform _parent;

        public async UniTask<IBeltItemObject> CreateAsync(ItemId itemId, Vector3 position)
        {
            var itemMaster = MasterHolder.ItemMaster.GetItemMaster(itemId);
            var modelPath = itemMaster.AddressablePaths?.EntityModel;
            if (string.IsNullOrEmpty(modelPath)) return await CreateTextured();

            var modelPrefab = await ModelPrefabTask();
            if (modelPrefab == null) return await CreateTextured();
            return new GameObjectBeltItemObject(Object.Instantiate(modelPrefab, position, Quaternion.identity, Parent()));

            #region Internal

            async UniTask<IBeltItemObject> CreateTextured()
            {
                _defaultItemPrefabTask ??= LoadDefaultItemPrefab().Preserve();
                var defaultItemPrefab = await _defaultItemPrefabTask.Value;
                // 既定prefabが読めなければ空の置き物で表示枠だけを保つ(理由は読み込み時に1回ログ済み)
                // Without the default prefab keep only an empty placeholder (the reason was logged once at load)
                if (defaultItemPrefab == null)
                {
                    var placeholder = new GameObject(MissingItemObjectName);
                    placeholder.transform.SetParent(Parent(), false);
                    placeholder.transform.position = position;
                    return new GameObjectBeltItemObject(placeholder);
                }

                var itemObject = Object.Instantiate(defaultItemPrefab, position, Quaternion.identity, Parent());
                var viewData = ClientContext.ItemImageContainer.GetItemView(itemId);
                itemObject.GetComponent<BeltItemTexturedObject>().SetTexture(viewData?.ItemTexture, itemId);
                return new GameObjectBeltItemObject(itemObject);
            }

            UniTask<GameObject> ModelPrefabTask()
            {
                if (_modelPrefabTaskByItem.TryGetValue(itemId, out var cached)) return cached;
                var task = LoadModelPrefab().Preserve();
                _modelPrefabTaskByItem[itemId] = task;
                return task;
            }

            // Addressableの読み込み結果は外部データ。失敗は種類ごとに1回だけログに残し、以後は既定のprefabへ落とす
            // The Addressable load result is external data; a failure is logged once per kind and later falls back to the default prefab
            async UniTask<GameObject> LoadModelPrefab()
            {
                var loaded = await AddressableLoader.LoadAsync<GameObject>(modelPath);
                if (loaded?.Asset != null) return loaded.Asset;
                Debug.LogError($"[BeltTransportView] failed to load the item model {modelPath} for item {itemId.AsPrimitive()}; showing the textured default instead.");
                return null;
            }

            // 既定prefabの読み込み結果も外部データ。失敗は1回だけログに残す
            // The default prefab load result is external data too; a failure is logged once
            async UniTask<GameObject> LoadDefaultItemPrefab()
            {
                var prefab = await AddressableLoader.LoadAsyncDefault<GameObject>(DefaultItemPrefabPath);
                if (prefab == null) Debug.LogError($"[BeltTransportView] failed to load the default item prefab {DefaultItemPrefabPath}; belt items are shown as empty placeholders.");
                return prefab;
            }

            #endregion
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
