using System.Collections.Generic;
using Client.Common.Asset;
using Client.Game.InGame.Context;
using Core.Master;
using Cysharp.Threading.Tasks;
using UnityEngine;
namespace Client.Game.InGame.Entity.Factory
{
    public sealed class BeltItemPrefabLoader : IBeltItemPrefabLoader
    {
        private const string DefaultItemPrefabPath = "Vanilla/Game/ItemEntity";
        private readonly Dictionary<ItemId, GameObject> _customModels = new();
        private GameObject _defaultPrefab;
        public async UniTask<BeltItemPrefab> LoadAsync(ItemId itemId)
        {
            // 既存masterのモデル指定を優先し、ロード済みPrefabを共有する。
            // Prefer the existing master model setting and share loaded prefabs.
            var master = MasterHolder.ItemMaster.GetItemMaster(itemId);
            var path = master.AddressablePaths?.EntityModel;
            if (!string.IsNullOrEmpty(path))
            {
                if (_customModels.TryGetValue(itemId, out var cached)) return new BeltItemPrefab(cached, null, true);
                var loaded = await AddressableLoader.LoadAsync<GameObject>(path);
                if (loaded?.Asset != null)
                {
                    _customModels[itemId] = loaded.Asset;
                    return new BeltItemPrefab(loaded.Asset, null, true);
                }
                Debug.LogError($"Failed to load custom entity model: {path}. Falling back to texture-based display.");
            }
            // 標準表示も従来のPrefabとアイテム画像を使う。
            // Standard items retain the existing prefab and item image.
            if (_defaultPrefab == null) _defaultPrefab = await AddressableLoader.LoadAsyncDefault<GameObject>(DefaultItemPrefabPath);
            var view = ClientContext.ItemImageContainer.GetItemView(itemId);
            return new BeltItemPrefab(_defaultPrefab, view?.ItemTexture, false);
        }
    }
}
