using UnityEngine;

namespace Client.Game.InGame.Map.MapObject
{
    public class MapObjectRayTarget : MonoBehaviour
    {
        public MapObjectGameObject MapObjectGameObject { get; private set; }
        
        public void Initialize(MapObjectGameObject mapObjectGameObject)
        {
            MapObjectGameObject = mapObjectGameObject;
        }
    }
}