using Client.Game.InGame.Interact;
using UnityEngine;

namespace Client.Game.InGame.Map.MapObject
{
    public class MapObjectRayTarget : MonoBehaviour, IInteractRayTarget
    {
        public MapObjectGameObject MapObjectGameObject { get; private set; }

        public IInteractable Interactable => MapObjectGameObject;

        public void Initialize(MapObjectGameObject mapObjectGameObject)
        {
            MapObjectGameObject = mapObjectGameObject;
        }
    }
}
