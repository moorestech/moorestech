using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Client.Game.InGame.Context
{
    public class DIContainer
    {
        public static IObjectResolver DIContainerResolver { get; private set; }
        
        public DIContainer(IObjectResolver objectResolver)
        {
            DIContainerResolver = objectResolver;
        }
        
        public static GameObject Instantiate(GameObject prefab)
        {
            return DIContainerResolver.Instantiate(prefab);
        }
        
        public static GameObject Instantiate(GameObject prefab, Transform parent, bool worldPositionStays = false)
        {
            return DIContainerResolver.Instantiate(prefab, parent, worldPositionStays);
        }
        
        public static GameObject Instantiate(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            return DIContainerResolver.Instantiate(prefab, position, rotation);
        }
        
        public static GameObject Instantiate(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent)
        {
            return DIContainerResolver.Instantiate(prefab, position, rotation, parent);
        }
        
        public static void InjectGameObject(GameObject gameObject)
        {
            DIContainerResolver.InjectGameObject(gameObject);
        }
    }
}