using Client.Game.InGame.Entity.Object;
using UnityEngine;

namespace Client.Game.InGame.BeltTransport.View
{
    // 既定のアイテムprefabの表示物。prefabの部品が毎フレーム自分の目標位置へ寄せるので、位置は部品へ直接置く
    // The displayed object of the default item prefab; its component pulls toward its own target every frame, so the position is set through the component
    public sealed class PrefabBeltItemObject : IBeltItemObject
    {
        private readonly BeltConveyorItemEntityObject _entityObject;

        public PrefabBeltItemObject(BeltConveyorItemEntityObject entityObject)
        {
            _entityObject = entityObject;
        }

        public void SetPosition(Vector3 position)
        {
            _entityObject.SetDirectPosition(position);
        }

        public void Destroy()
        {
            _entityObject.Destroy();
        }
    }
}
