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

        // レイに乗せるか否かをコライダーの有効/無効で切り替える。歩行用の物理コライダーは別オブジェクトなので影響しない
        // Toggle whether this counts as a ray hit via the collider's enabled state; the walking collider lives on another object and is untouched
        public void SetInteractable(bool interactable)
        {
            GetComponent<Collider>().enabled = interactable;
        }
    }
}
