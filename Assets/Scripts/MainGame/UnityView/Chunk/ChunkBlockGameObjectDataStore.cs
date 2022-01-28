using MainGame.UnityView.Interface;
using UnityEngine;
using VContainer;

namespace MainGame.UnityView.Chunk
{
    public class ChunkBlockGameObjectDataStore : MonoBehaviour
    {
        private IPlaceBlockGameObject _placeBlockGameObject;

        [Inject]
        public void Construct(IPlaceBlockGameObject placeBlockGameObject)
        {
            _placeBlockGameObject = placeBlockGameObject;
            placeBlockGameObject.Subscribe(OnBlockPlaceEvent,OnBlockRemoveEvent);
        }


        private void OnBlockPlaceEvent(Vector2Int blockPosition, int blockId)
        {
            
        }

        private void OnBlockRemoveEvent(Vector2Int blockPosition)
        {
            
        }
    }
}
