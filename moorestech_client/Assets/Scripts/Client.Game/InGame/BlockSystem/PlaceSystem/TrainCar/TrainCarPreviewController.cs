using Core.Master;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.TrainCar
{
    public class TrainCarPreviewController : MonoBehaviour
    {
        public void ShowPreview(ItemId itemId, Vector3 position, Quaternion rotation, bool isPlaceable)
        {
            
        }
        public void SetActive(bool isActive)
        {
            gameObject.SetActive(isActive);
        }
    }
}