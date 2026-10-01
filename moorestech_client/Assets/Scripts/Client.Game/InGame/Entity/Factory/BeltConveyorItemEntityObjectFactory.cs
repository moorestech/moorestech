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
        public UniTask<IEntityObject> CreateEntity(Transform parent, EntityResponse entity)
        {
            var state = MessagePackSerializer.Deserialize<BeltConveyorItemEntityStateMessagePack>(entity.EntityData);
            return CreateItem(parent, entity.InstanceId, new ItemId(state.ItemId), entity.Position);
        }
        public async UniTask<IEntityObject> CreateItem(Transform parent, long identity, ItemId itemId, Vector3 position)
        {
            var asset = await _prefabs.LoadAsync(itemId);
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
            return view;
        }
    }
}
