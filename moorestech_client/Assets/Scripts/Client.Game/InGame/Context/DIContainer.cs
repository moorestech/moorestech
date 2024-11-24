using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Client.Game.InGame.Context
{
    public class DIContainer
    {
        public IObjectResolver DIContainerResolver { get; private set; }
        
        public DIContainer(IObjectResolver objectResolver)
        {
            DIContainerResolver = objectResolver;
        }
        
        public GameObject Instantiate(GameObject prefab)
        {
            return DIContainerResolver.Instantiate(prefab);
        }
        
        public T Instantiate<T>(T prefab, Transform parent, bool worldPositionStays = false) where T : Object
        {
            return DIContainerResolver.Instantiate(prefab, parent, worldPositionStays);
        }
        
        public T Instantiate<T>(T prefab, Vector3 position, Quaternion rotation) where T : Object
        {
            return DIContainerResolver.Instantiate(prefab, position, rotation);
        }
        
        public T Instantiate<T>(T prefab, Vector3 position, Quaternion rotation, Transform parent) where T : Object
        {
            return DIContainerResolver.Instantiate(prefab, position, rotation, parent);
        }
        
        public void InjectGameObject(GameObject gameObject)
        {
            DIContainerResolver.InjectGameObject(gameObject);
        }
    }
}