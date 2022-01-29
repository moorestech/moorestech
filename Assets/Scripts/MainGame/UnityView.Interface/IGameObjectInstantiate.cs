using UnityEngine;

namespace MainGame.UnityView.Interface
{
    public interface IGameObjectInstantiate
    {
        public void Instantiate(GameObject prefab, Vector3 position, Quaternion rotation,Transform parent);
    }
}