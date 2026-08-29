using Client.Game.InGame.Mining;
using UnityEngine;

namespace Client.Game.InGame.Map.MapObject
{
    [RequireComponent(typeof(Collider))]
    public class MapObjectRayTarget : MonoBehaviour, IMiningRayTarget
    {
        public MapObjectGameObject MapObjectGameObject { get; private set; }

        public IMiningTargetObject MiningTargetObject => MapObjectGameObject;

        public void Initialize(MapObjectGameObject mapObjectGameObject)
        {
            MapObjectGameObject = mapObjectGameObject;
        }

        // コライダー有効/無効を切替。歩行用は別objectで影響なし
        // Toggles via the collider's enabled state; the walking collider is a separate object
        internal void SetInteractable(bool interactable)
        {
            GetComponent<Collider>().enabled = interactable;
        }
    }
}
