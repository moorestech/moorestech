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
    }
}
