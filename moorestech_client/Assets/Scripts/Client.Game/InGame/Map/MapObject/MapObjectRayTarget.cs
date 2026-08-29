using Client.Game.InGame.Mining;
using UnityEngine;

namespace Client.Game.InGame.Map.MapObject
{
    public class MapObjectRayTarget : MonoBehaviour, IMiningRayTarget
    {
        public MapObjectGameObject MapObjectGameObject { get; private set; }

        public IMiningTargetObject MiningTargetObject => MapObjectGameObject;

        public void Initialize(MapObjectGameObject mapObjectGameObject)
        {
            MapObjectGameObject = mapObjectGameObject;
        }

        // 装飾物はレイに乗せない。歩行用の物理コライダーは別オブジェクトなので影響しない
        // A decoration stays off the ray; the walking collider lives on another object and is untouched
        public void SetInteractable(bool interactable)
        {
            GetComponent<Collider>().enabled = interactable;
        }
    }
}
