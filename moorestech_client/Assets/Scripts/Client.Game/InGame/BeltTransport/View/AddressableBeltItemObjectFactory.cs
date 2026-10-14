using System.Collections.Generic;
using Client.Common.Asset;
using Client.Game.InGame.Context;
using CommandForgeGenerator.Command;
using Core.Master;
using UnityEngine;

namespace Client.Game.InGame.BeltTransport.View
{
    // 種類にモデルがあればそのprefab、無ければテクスチャを貼る既定のアイテムprefabを置く。prefabは種類ごとに初回だけ同期で読む(列車のTrainCarObjectFactoryと同形)
    // Places the kind's model prefab when it has one, otherwise the default item prefab with the kind's texture; each prefab is loaded synchronously once per kind (same shape as TrainCarObjectFactory)
    // 置いた表示物は1つの親の下に集め、スキットのエンティティ窓口はその親ごと表示を切り替える
    // Placed objects are gathered under one parent, and the skit's entity window toggles that parent as a whole
    public sealed class AddressableBeltItemObjectFactory : IBeltItemObjectFactory, ISkitEntityObjectControl
    {
        private const string DefaultItemPrefabPath = "Vanilla/Game/ItemEntity";
        private const string ParentName = "BeltTransportItems";
        private const string MissingItemObjectName = "MissingBeltItem";

        // 読み込み結果を種類ごとに持つ。失敗(null)もそのまま印として残し、読み直さない
        // Holds the load result per kind; a failure (null) stays as the mark and is never retried
        private bool _isDefaultItemPrefabLoaded;
        private GameObject _defaultItemPrefab;
        private readonly Dictionary<ItemId, GameObject> _modelPrefabByItem = new();
        private Transform _parent;

        public IBeltItemObject Create(ItemId itemId, Vector3 position)
        {
            var modelPrefab = ResolveModelPrefab();
            if (modelPrefab != null) return new GameObjectBeltItemObject(Object.Instantiate(modelPrefab, position, Quaternion.identity, Parent()));
            return CreateTextured();

            #region Internal

            // Addressableの読み込み結果は外部データ。失敗は種類ごとに1回だけログに残し、以後は既定のprefabへ落とす
            // The Addressable load result is external data; a failure is logged once per kind and later falls back to the default prefab
            GameObject ResolveModelPrefab()
            {
                if (_modelPrefabByItem.TryGetValue(itemId, out var cached)) return cached;
                var modelPath = MasterHolder.ItemMaster.GetItemMaster(itemId).AddressablePaths?.EntityModel;
                var loaded = string.IsNullOrEmpty(modelPath) ? null : AddressableLoader.LoadDefault<GameObject>(modelPath);
                if (!string.IsNullOrEmpty(modelPath) && loaded == null) Debug.LogError($"[BeltTransportView] failed to load the item model {modelPath} for item {itemId.AsPrimitive()}; showing the textured default instead.");
                _modelPrefabByItem[itemId] = loaded;
                return loaded;
            }

            IBeltItemObject CreateTextured()
            {
                var defaultItemPrefab = ResolveDefaultItemPrefab();
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

            // 既定prefabの読み込み結果も外部データ。失敗は1回だけログに残す
            // The default prefab load result is external data too; a failure is logged once
            GameObject ResolveDefaultItemPrefab()
            {
                if (_isDefaultItemPrefabLoaded) return _defaultItemPrefab;
                _isDefaultItemPrefabLoaded = true;
                _defaultItemPrefab = AddressableLoader.LoadDefault<GameObject>(DefaultItemPrefabPath);
                if (_defaultItemPrefab == null) Debug.LogError($"[BeltTransportView] failed to load the default item prefab {DefaultItemPrefabPath}; belt items are shown as empty placeholders.");
                return _defaultItemPrefab;
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
