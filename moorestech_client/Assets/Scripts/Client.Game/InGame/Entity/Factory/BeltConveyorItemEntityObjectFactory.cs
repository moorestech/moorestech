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
        private readonly IBeltItemPrefabLoader _prefabs;
        public BeltConveyorItemEntityObjectFactory(IBeltItemPrefabLoader prefabs) { _prefabs = prefabs; }
        public async UniTask<IEntityObject> CreateEntity(Transform parent, EntityResponse entity)
        {
            var state = MessagePackSerializer.Deserialize<BeltConveyorItemEntityStateMessagePack>(entity.EntityData);
            var result = await CreateItem(parent, entity.InstanceId, new ItemId(state.ItemId), entity.Position);
            if (!result.Succeeded) throw new System.InvalidOperationException(result.FailureReason);
            return result.View;
        }
        public async UniTask<BeltItemCreationResult> CreateItem(Transform parent, long identity, ItemId itemId, Vector3 position)
        {
            var loaded = await _prefabs.LoadAsync(itemId);
            if (!loaded.Succeeded) return BeltItemCreationResult.Missing(loaded.FailureReason);
            var asset = loaded.Prefab;
            // 非表示の親の下で生成し、初期化直後から同じ表示状態を継承する。
            // Instantiate under the existing parent to inherit its visibility immediately.
            var instance = UnityEngine.Object.Instantiate(asset.Prefab, position, Quaternion.identity, parent);
            IEntityObject view;
            if (asset.CustomModel) view = instance.AddComponent<CustomModelBeltConveyorItemEntityObject>();
            else
            {
                var textured = instance.GetComponent<BeltConveyorItemEntityObject>();
                textured.SetTexture(asset.Texture, itemId);
                view = textured;
            }
            view.Initialize(identity);
            view.SetDirectPosition(position);
            return BeltItemCreationResult.Created(view);
        }
    }
}
