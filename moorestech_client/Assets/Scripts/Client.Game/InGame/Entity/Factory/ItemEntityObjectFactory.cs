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
            // StateデータからItemIdを復元してマスター情報を取得
            // Restore ItemId from state data and get master data
            var itemState = DeserializeState();
            var id = new ItemId(itemState.ItemId);
            var itemMaster = MasterHolder.ItemMaster.GetItemMaster(id);

            // テクスチャの取得と適用可否を判定
            // Get texture and determine whether to apply
            var viewData = ClientContext.ItemImageContainer.GetItemView(id);
            var texture = viewData != null ? viewData.ItemTexture : null;

            // アドレッサブルのプレハブを優先して取得（手持ち用と分離された経路を考慮）
            // Prefer loading addressable prefab (considering dedicated entity model path)
            using var loadedAsset = await LoadEntityModelAsync(itemMaster);
            var prefab = loadedAsset?.Asset != null ? loadedAsset.Asset : _itemPrefab;

            // エンティティを生成し親と座標を設定
            // Instantiate entity and set parent with position
            var itemObject = GameObject.Instantiate(prefab, entity.Position, Quaternion.identity, parent);
            var item = itemObject.GetComponent<ItemEntityObject>() ?? itemObject.AddComponent<ItemEntityObject>();

            var shouldApplyTexture = prefab == _itemPrefab || texture != null;
            if (shouldApplyTexture) item.SetTexture(texture);
            return item;

            #region Internal

            ItemEntityStateMessagePack DeserializeState()
            {
                // データが空の場合は既定値を返す
                // Return default state when data is empty
                if (entity.EntityData == null || entity.EntityData.Length == 0) return new ItemEntityStateMessagePack();
                return MessagePackSerializer.Deserialize<ItemEntityStateMessagePack>(entity.EntityData);
            }

            async UniTask<LoadedAsset<GameObject>> LoadEntityModelAsync(ItemMasterElement itemMaster)
            {
                // エンティティ専用パスを優先し、未設定なら手持ち用パスを使用
                // Prefer entity-specific path, fall back to hand-grab path when empty
                var addressablePath = string.IsNullOrEmpty(itemMaster.ItemEntityModelAddressablePath)
                    ? itemMaster.HandGrabModelAddressablePath
                    : itemMaster.ItemEntityModelAddressablePath;

                // アドレッサブルパスが未設定なら共通Prefabを使用
                // Use common prefab when addressable path is empty
                if (string.IsNullOrEmpty(addressablePath)) return null;

                var loadedAsset = await AddressableLoader.LoadAsync<GameObject>(addressablePath);
                if (loadedAsset?.Asset == null) Debug.LogError($"Failed to load item entity model: {addressablePath}");
                return loadedAsset;
            }

            #endregion
        }
    }
}
